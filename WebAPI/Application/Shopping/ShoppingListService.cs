using InventoryAPI.Contracts.Shopping;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Shopping;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Application.Shopping;

/// <summary>
/// 現在有効な買い物リストの作成、更新、補充提案を担当します。
/// </summary>
public sealed class ShoppingListService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<ShoppingListResponse> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var list = await FindCurrentAsync(cancellationToken);
        return list is null ? EmptyResponse() : ToResponse(list);
    }

    public async Task<ShoppingListResponse?> AddItemAsync(
        AddShoppingItemRequest request,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        if (request.ProductId.HasValue && !await ProductExistsAsync(request.ProductId.Value, cancellationToken))
            return null;

        var list = await GetOrCreateCurrentAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = list.Id,
            ProductId = request.ProductId,
            Name = normalizedName,
            Quantity = request.Quantity,
            Source = ShoppingItemSource.Manual,
            Status = ShoppingItemStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        list.Items.Add(item);
        db.ShoppingListItems.Add(item);

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(list);
    }

    public async Task<ShoppingListResponse> GenerateSuggestionsAsync(CancellationToken cancellationToken)
    {
        var list = await GetOrCreateCurrentAsync(cancellationToken);
        var quantities = await GetQuantityByProductAsync(cancellationToken);
        var products = await db.Products
            .Where(product => product.HouseholdId == SystemDefaults.HouseholdId && product.ReorderPoint != null)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var product in products)
        {
            var currentQuantity = quantities.GetValueOrDefault(product.Id);
            if (currentQuantity >= product.ReorderPoint!.Value)
                continue;

            var existingSuggestion = list.Items.FirstOrDefault(item =>
                item.ProductId == product.Id && item.Source == ShoppingItemSource.ReorderSuggestion);

            // 購入済み・却下済みの提案は、同じ有効リスト内では利用者の判断を優先して復活させません。
            if (existingSuggestion is { Status: ShoppingItemStatus.Dismissed or ShoppingItemStatus.Purchased })
                continue;

            var targetQuantity = product.TargetQuantity ?? product.ReorderPoint.Value;
            var suggestedQuantity = Math.Max(targetQuantity - currentQuantity, 1);
            if (existingSuggestion is null)
            {
                var suggestion = CreateSuggestion(
                    list.Id,
                    product.Id,
                    product.Name,
                    suggestedQuantity,
                    now);
                list.Items.Add(suggestion);
                db.ShoppingListItems.Add(suggestion);
                continue;
            }

            existingSuggestion.Name = product.Name;
            existingSuggestion.Quantity = suggestedQuantity;
            existingSuggestion.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(list);
    }

    public async Task<ShoppingListResponse?> UpdateItemAsync(
        Guid itemId,
        UpdateShoppingItemRequest request,
        CancellationToken cancellationToken)
    {
        var list = await FindCurrentAsync(cancellationToken);
        var item = list?.Items.SingleOrDefault(candidate => candidate.Id == itemId);
        if (list is null || item is null)
            return null;

        if (request.Quantity.HasValue)
            item.Quantity = request.Quantity.Value;
        if (request.Status.HasValue)
            item.Status = request.Status.Value;
        item.UpdatedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(list);
    }

    public async Task<ShoppingListResponse?> CompleteAsync(CancellationToken cancellationToken)
    {
        var list = await FindCurrentAsync(cancellationToken);
        if (list is null)
            return null;

        list.Status = ShoppingListStatus.Completed;
        list.CompletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(list);
    }

    private async Task<ShoppingList?> FindCurrentAsync(CancellationToken cancellationToken) =>
        await db.ShoppingLists
            .Include(list => list.Items)
            .SingleOrDefaultAsync(
                list => list.HouseholdId == SystemDefaults.HouseholdId &&
                        list.Status == ShoppingListStatus.Active,
                cancellationToken);

    private async Task<ShoppingList> GetOrCreateCurrentAsync(CancellationToken cancellationToken)
    {
        var existing = await FindCurrentAsync(cancellationToken);
        if (existing is not null)
            return existing;

        var list = new ShoppingList
        {
            Id = Guid.NewGuid(),
            HouseholdId = SystemDefaults.HouseholdId,
            Status = ShoppingListStatus.Active,
            CreatedAt = timeProvider.GetUtcNow()
        };
        db.ShoppingLists.Add(list);
        return list;
    }

    private async Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken) =>
        await db.Products.AnyAsync(
            product => product.Id == productId && product.HouseholdId == SystemDefaults.HouseholdId,
            cancellationToken);

    private async Task<Dictionary<Guid, decimal>> GetQuantityByProductAsync(CancellationToken cancellationToken)
    {
        var rows = await db.StockLots.AsNoTracking()
            .Where(lot => lot.HouseholdId == SystemDefaults.HouseholdId)
            .Select(lot => new { lot.ProductId, lot.CurrentQuantity })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(row => row.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.CurrentQuantity));
    }

    private static ShoppingListItem CreateSuggestion(
        Guid shoppingListId,
        Guid productId,
        string productName,
        decimal quantity,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ShoppingListId = shoppingListId,
            ProductId = productId,
            Name = productName,
            Quantity = quantity,
            Source = ShoppingItemSource.ReorderSuggestion,
            Status = ShoppingItemStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static ShoppingListResponse EmptyResponse() =>
        new(null, ShoppingListStatus.Active, null, []);

    private static ShoppingListResponse ToResponse(ShoppingList list) => new(
        list.Id,
        list.Status,
        list.CreatedAt,
        list.Items
            .OrderBy(item => item.Status)
            .ThenBy(item => item.Name)
            .Select(item => new ShoppingItemResponse(
                item.Id,
                item.ProductId,
                item.Name,
                item.Quantity,
                item.Source,
                item.Status))
            .ToList());
}
