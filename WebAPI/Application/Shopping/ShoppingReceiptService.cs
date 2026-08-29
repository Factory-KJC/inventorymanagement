using System.Data;
using System.Security.Cryptography;
using System.Text;
using InventoryAPI.Contracts.Shopping;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Inventory;
using InventoryAPI.Domain.Shopping;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Application.Shopping;

/// <summary>
/// 完了した買い物リストの商品を、保管場所・期限別の実在庫へ一括変換します。
/// </summary>
public sealed class ShoppingReceiptService(ApplicationDbContext db, TimeProvider timeProvider)
{
    /// <summary>
    /// 購入済みかつ商品に紐付く全項目を、一度の入庫操作として記録します。
    /// </summary>
    public async Task<ShoppingReceiptResult> ReceiveAsync(
        Guid shoppingListId,
        ReceiveShoppingListRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var operationKey = CreateOperationKey(shoppingListId, idempotencyKey);
        var existingOperation = await db.StockOperations
            .Include(operation => operation.Movements)
            .SingleOrDefaultAsync(
                operation => operation.HouseholdId == SystemDefaults.HouseholdId &&
                             operation.IdempotencyKey == operationKey,
                cancellationToken);
        if (existingOperation is not null)
            return ShoppingReceiptResult.Success(existingOperation);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var list = await db.ShoppingLists
            .Include(candidate => candidate.Items)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == shoppingListId &&
                             candidate.HouseholdId == SystemDefaults.HouseholdId,
                cancellationToken);
        if (list is null)
            return ShoppingReceiptResult.NotFound();
        if (list.Status != ShoppingListStatus.Completed)
            return ShoppingReceiptResult.InvalidState();

        var purchasedItems = list.Items
            .Where(item => item.Status == ShoppingItemStatus.Purchased && item.ProductId.HasValue)
            .ToDictionary(item => item.Id);
        var requestedItems = request.Items
            .GroupBy(item => item.ItemId)
            .ToDictionary(group => group.Key, group => group.First());
        if (purchasedItems.Count == 0 ||
            request.Items.Count != requestedItems.Count ||
            !purchasedItems.Keys.ToHashSet().SetEquals(requestedItems.Keys))
            return ShoppingReceiptResult.InvalidItems();

        var locationIds = requestedItems.Values.Select(item => item.LocationId).Distinct().ToList();
        var existingLocationCount = await db.Locations.CountAsync(
            location => location.HouseholdId == SystemDefaults.HouseholdId &&
                        locationIds.Contains(location.Id),
            cancellationToken);
        if (existingLocationCount != locationIds.Count)
            return ShoppingReceiptResult.NotFound();

        var now = timeProvider.GetUtcNow();
        var operation = new StockOperation
        {
            Id = Guid.NewGuid(),
            HouseholdId = SystemDefaults.HouseholdId,
            IdempotencyKey = operationKey,
            Type = StockMovementType.Receive,
            RequestedQuantity = purchasedItems.Values.Sum(item => item.Quantity),
            OccurredAt = now
        };

        foreach (var purchasedItem in purchasedItems.Values)
        {
            var destination = requestedItems[purchasedItem.Id];
            var productId = purchasedItem.ProductId!.Value;
            var lot = await db.StockLots.SingleOrDefaultAsync(candidate =>
                candidate.HouseholdId == SystemDefaults.HouseholdId &&
                candidate.ProductId == productId &&
                candidate.LocationId == destination.LocationId &&
                candidate.ExpiresOn == destination.ExpiresOn,
                cancellationToken);
            if (lot is null)
            {
                lot = new StockLot
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = SystemDefaults.HouseholdId,
                    ProductId = productId,
                    LocationId = destination.LocationId,
                    ExpiresOn = destination.ExpiresOn,
                    CreatedAt = now
                };
                db.StockLots.Add(lot);
            }

            lot.CurrentQuantity += purchasedItem.Quantity;
            lot.UpdatedAt = now;
            operation.Movements.Add(new StockMovement
            {
                StockOperationId = operation.Id,
                StockLotId = lot.Id,
                ProductId = productId,
                LocationId = destination.LocationId,
                Type = StockMovementType.Receive,
                QuantityDelta = purchasedItem.Quantity,
                OccurredAt = now,
                Note = $"買い物リスト「{purchasedItem.Name}」から入庫"
            });
        }

        list.Status = ShoppingListStatus.Received;
        db.StockOperations.Add(operation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ShoppingReceiptResult.Success(operation);
    }

    private static string CreateOperationKey(Guid shoppingListId, string idempotencyKey) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"shopping-receive:{shoppingListId:N}:{idempotencyKey}")));
}
