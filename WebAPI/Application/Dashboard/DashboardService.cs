using InventoryAPI.Contracts.Dashboard;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Shopping;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Application.Dashboard;

/// <summary>
/// ダッシュボードの集計と一覧表示用データの構築を担当します。
/// </summary>
public sealed class DashboardService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 20;
    private const int ExpirationWarningDays = 7;

    public async Task<DashboardResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var productCount = await db.Products.CountAsync(
            product => product.HouseholdId == SystemDefaults.HouseholdId,
            cancellationToken);
        var quantities = await GetQuantityByProductAsync(cancellationToken);
        var reorderProducts = await db.Products.AsNoTracking()
            .Where(product => product.HouseholdId == SystemDefaults.HouseholdId && product.ReorderPoint != null)
            .Select(product => new { product.Id, ReorderPoint = product.ReorderPoint!.Value })
            .ToListAsync(cancellationToken);
        var lowStockCount = reorderProducts.Count(
            product => quantities.GetValueOrDefault(product.Id) < product.ReorderPoint);

        var (today, deadline) = GetExpirationPeriod();
        var expiringSoonCount = await db.StockLots.CountAsync(lot =>
            lot.HouseholdId == SystemDefaults.HouseholdId &&
            lot.CurrentQuantity > 0 &&
            lot.ExpiresOn != null &&
            lot.ExpiresOn >= today &&
            lot.ExpiresOn <= deadline,
            cancellationToken);
        var shoppingItemCount = await CurrentPendingShoppingItems().CountAsync(cancellationToken);

        return new DashboardResponse(productCount, lowStockCount, expiringSoonCount, shoppingItemCount);
    }

    public async Task<DashboardListResponse?> GetListAsync(
        string category,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = category.ToLowerInvariant();
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var items = normalizedCategory switch
        {
            DashboardCategories.Products => await GetProductsAsync(cancellationToken),
            DashboardCategories.LowStock => await GetLowStockAsync(cancellationToken),
            DashboardCategories.Expiring => await GetExpiringAsync(cancellationToken),
            DashboardCategories.Shopping => await GetShoppingAsync(cancellationToken),
            _ => null
        };

        if (items is null)
            return null;

        // 現在のデータ量では各一覧を一度集計してからページングする方が単純です。
        // 件数増加時はカテゴリごとのDBページングへ置き換えられるよう、このサービス内に閉じています。
        var pageItems = items
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();
        return new DashboardListResponse(
            normalizedCategory,
            normalizedPage,
            normalizedPageSize,
            items.Count,
            pageItems);
    }

    private async Task<List<DashboardItemResponse>> GetProductsAsync(CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking()
            .Where(product => product.HouseholdId == SystemDefaults.HouseholdId)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);
        var quantities = await GetQuantityByProductAsync(cancellationToken);

        return products.Select(product => new DashboardItemResponse(
            product.Id,
            product.Name,
            product.Barcode is null ? product.Unit : $"JAN {product.Barcode} · {product.Unit}",
            quantities.GetValueOrDefault(product.Id))).ToList();
    }

    private async Task<List<DashboardItemResponse>> GetLowStockAsync(CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking()
            .Where(product => product.HouseholdId == SystemDefaults.HouseholdId && product.ReorderPoint != null)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);
        var quantities = await GetQuantityByProductAsync(cancellationToken);

        return products
            .Where(product => quantities.GetValueOrDefault(product.Id) < product.ReorderPoint!.Value)
            .Select(product => new DashboardItemResponse(
                product.Id,
                product.Name,
                $"補充点 {product.ReorderPoint} {product.Unit} / 目標 {product.TargetQuantity ?? product.ReorderPoint} {product.Unit}",
                quantities.GetValueOrDefault(product.Id)))
            .ToList();
    }

    private async Task<List<DashboardItemResponse>> GetExpiringAsync(CancellationToken cancellationToken)
    {
        var (today, deadline) = GetExpirationPeriod();
        return await (
            from lot in db.StockLots.AsNoTracking()
            join product in db.Products.AsNoTracking() on lot.ProductId equals product.Id
            join location in db.Locations.AsNoTracking() on lot.LocationId equals location.Id
            where lot.HouseholdId == SystemDefaults.HouseholdId &&
                  lot.CurrentQuantity > 0 &&
                  lot.ExpiresOn != null &&
                  lot.ExpiresOn >= today &&
                  lot.ExpiresOn <= deadline
            orderby lot.ExpiresOn, product.Name
            select new DashboardItemResponse(
                lot.Id,
                product.Name,
                $"期限 {lot.ExpiresOn} · {location.Name} · {product.Unit}",
                lot.CurrentQuantity))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<DashboardItemResponse>> GetShoppingAsync(CancellationToken cancellationToken) =>
        await CurrentPendingShoppingItems()
            .OrderBy(item => item.Name)
            .Select(item => new DashboardItemResponse(
                item.Id,
                item.Name,
                item.Source == ShoppingItemSource.ReorderSuggestion ? "在庫から提案" : "手動追加",
                item.Quantity))
            .ToListAsync(cancellationToken);

    private IQueryable<ShoppingListItem> CurrentPendingShoppingItems() =>
        from item in db.ShoppingListItems.AsNoTracking()
        join list in db.ShoppingLists.AsNoTracking() on item.ShoppingListId equals list.Id
        where list.HouseholdId == SystemDefaults.HouseholdId &&
              list.Status == ShoppingListStatus.Active &&
              item.Status == ShoppingItemStatus.Pending
        select item;

    private async Task<Dictionary<Guid, decimal>> GetQuantityByProductAsync(CancellationToken cancellationToken)
    {
        var rows = await db.StockLots.AsNoTracking()
            .Where(lot => lot.HouseholdId == SystemDefaults.HouseholdId)
            .Select(lot => new { lot.ProductId, lot.CurrentQuantity })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(row => row.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.CurrentQuantity));
    }

    private (DateOnly Today, DateOnly Deadline) GetExpirationPeriod()
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return (today, today.AddDays(ExpirationWarningDays));
    }
}

/// <summary>
/// URLで使用するダッシュボードカテゴリ名を一元管理します。
/// </summary>
public static class DashboardCategories
{
    public const string Products = "products";
    public const string LowStock = "low-stock";
    public const string Expiring = "expiring";
    public const string Shopping = "shopping";
}
