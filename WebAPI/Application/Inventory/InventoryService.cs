using System.Data;
using InventoryAPI.Contracts.Inventory;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Application.Inventory;

/// <summary>
/// 在庫の入庫・消費を、操作履歴と整合性を保ちながら実行します。
/// </summary>
public sealed class InventoryService(ApplicationDbContext db, TimeProvider timeProvider)
{
    /// <summary>
    /// 同一商品・保管場所・期限のロットへ数量を加算します。
    /// </summary>
    public async Task<InventoryResult> ReceiveAsync(
        ReceiveStockRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await FindOperationAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
            return InventoryResult.Success(existing);

        if (!await ProductAndLocationExistAsync(request.ProductId, request.LocationId, cancellationToken))
            return InventoryResult.NotFound();

        // ロットの同時作成や数量更新の競合を防ぐため、在庫更新は直列化可能トランザクションで行います。
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var lot = await db.StockLots.SingleOrDefaultAsync(x =>
            x.HouseholdId == SystemDefaults.HouseholdId &&
            x.ProductId == request.ProductId &&
            x.LocationId == request.LocationId &&
            x.ExpiresOn == request.ExpiresOn,
            cancellationToken);

        if (lot is null)
        {
            lot = new StockLot
            {
                Id = Guid.NewGuid(),
                HouseholdId = SystemDefaults.HouseholdId,
                ProductId = request.ProductId,
                LocationId = request.LocationId,
                ExpiresOn = request.ExpiresOn,
                CreatedAt = now
            };
            db.StockLots.Add(lot);
        }

        lot.CurrentQuantity += request.Quantity;
        lot.UpdatedAt = now;

        var operation = CreateOperation(idempotencyKey, StockMovementType.Receive, request.Quantity, now);
        operation.Movements.Add(CreateMovement(operation, lot, StockMovementType.Receive, request.Quantity, request.Note, now));
        db.StockOperations.Add(operation);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return InventoryResult.Success(operation);
    }

    public async Task<InventoryResult> ConsumeAsync(
        ConsumeStockRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        await RemoveAsync(
            request.ProductId, request.Quantity, request.LocationId, request.Note,
            StockMovementType.Consume, idempotencyKey, cancellationToken);

    public async Task<InventoryResult> DiscardAsync(
        DiscardStockRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        await RemoveAsync(
            request.ProductId, request.Quantity, request.LocationId, request.Note,
            StockMovementType.Discard, idempotencyKey, cancellationToken);

    public async Task<InventoryResult> AdjustAsync(
        AdjustStockRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await FindOperationAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
            return InventoryResult.Success(existing);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var lot = await db.StockLots.SingleOrDefaultAsync(x =>
            x.Id == request.LotId && x.HouseholdId == SystemDefaults.HouseholdId,
            cancellationToken);
        if (lot is null)
            return InventoryResult.NotFound();

        var now = timeProvider.GetUtcNow();
        var difference = request.CountedQuantity - lot.CurrentQuantity;
        lot.CurrentQuantity = request.CountedQuantity;
        lot.UpdatedAt = now;

        var operation = CreateOperation(idempotencyKey, StockMovementType.Adjust, request.CountedQuantity, now);
        operation.Movements.Add(CreateMovement(operation, lot, StockMovementType.Adjust, difference, request.Note, now));
        db.StockOperations.Add(operation);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return InventoryResult.Success(operation);
    }

    /// <summary>
    /// 指定した在庫操作を、元の履歴を保持した逆仕訳として取り消します。
    /// </summary>
    public async Task<InventoryResult> ReverseAsync(
        Guid operationId,
        ReverseStockOperationRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await FindOperationAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
            return InventoryResult.Success(existing);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var original = await db.StockOperations
            .Include(operation => operation.Movements)
            .SingleOrDefaultAsync(operation =>
                operation.Id == operationId && operation.HouseholdId == SystemDefaults.HouseholdId,
                cancellationToken);
        if (original is null)
            return InventoryResult.NotFound();
        if (original.Type == StockMovementType.Reverse)
            return InventoryResult.CannotReverse();

        var originalMovementIds = original.Movements.Select(movement => movement.Id).ToList();
        if (await db.StockMovements.AnyAsync(
                movement => movement.ReversesMovementId.HasValue &&
                            originalMovementIds.Contains(movement.ReversesMovementId.Value),
                cancellationToken))
            return InventoryResult.AlreadyReversed();

        var lotIds = original.Movements.Select(movement => movement.StockLotId).Distinct().ToList();
        var lots = await db.StockLots
            .Where(lot => lotIds.Contains(lot.Id) && lot.HouseholdId == SystemDefaults.HouseholdId)
            .ToDictionaryAsync(lot => lot.Id, cancellationToken);
        if (lots.Count != lotIds.Count)
            return InventoryResult.NotFound();

        foreach (var movement in original.Movements)
        {
            if (lots[movement.StockLotId].CurrentQuantity - movement.QuantityDelta < 0)
                return InventoryResult.CannotReverse();
        }

        var now = timeProvider.GetUtcNow();
        var operation = CreateOperation(
            idempotencyKey,
            StockMovementType.Reverse,
            original.RequestedQuantity,
            now);
        foreach (var movement in original.Movements)
        {
            var lot = lots[movement.StockLotId];
            var delta = -movement.QuantityDelta;
            lot.CurrentQuantity += delta;
            lot.UpdatedAt = now;
            var reversal = CreateMovement(operation, lot, StockMovementType.Reverse, delta, request.Note, now);
            reversal.ReversesMovementId = movement.Id;
            operation.Movements.Add(reversal);
        }

        db.StockOperations.Add(operation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return InventoryResult.Success(operation);
    }

    private async Task<InventoryResult> RemoveAsync(
        Guid productId,
        decimal quantity,
        Guid? locationId,
        string? note,
        StockMovementType movementType,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await FindOperationAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
            return InventoryResult.Success(existing);

        if (!await db.Products.AnyAsync(x => x.Id == productId && x.HouseholdId == SystemDefaults.HouseholdId, cancellationToken))
            return InventoryResult.NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        // FEFO: 期限ありを先に、さらに期限の近いロットから順に減算します。
        var lots = await db.StockLots
            .Where(x => x.HouseholdId == SystemDefaults.HouseholdId &&
                        x.ProductId == productId &&
                        x.CurrentQuantity > 0 &&
                        (!locationId.HasValue || x.LocationId == locationId.Value))
            .OrderBy(x => x.ExpiresOn == null)
            .ThenBy(x => x.ExpiresOn)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (lots.Sum(x => x.CurrentQuantity) < quantity)
            return InventoryResult.InsufficientStock();

        var now = timeProvider.GetUtcNow();
        var operation = CreateOperation(idempotencyKey, movementType, quantity, now);
        var remaining = quantity;

        foreach (var lot in lots)
        {
            if (remaining == 0)
                break;

            var consumed = Math.Min(lot.CurrentQuantity, remaining);
            lot.CurrentQuantity -= consumed;
            lot.UpdatedAt = now;
            remaining -= consumed;
            operation.Movements.Add(CreateMovement(operation, lot, movementType, -consumed, note, now));
        }

        db.StockOperations.Add(operation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return InventoryResult.Success(operation);
    }

    /// <summary>
    /// 同じ冪等キーの完了済み操作を返し、通信再送による二重計上を防ぎます。
    /// </summary>
    private async Task<StockOperation?> FindOperationAsync(string key, CancellationToken cancellationToken) =>
        await db.StockOperations.Include(x => x.Movements).SingleOrDefaultAsync(
            x => x.HouseholdId == SystemDefaults.HouseholdId && x.IdempotencyKey == key,
            cancellationToken);

    private async Task<bool> ProductAndLocationExistAsync(Guid productId, Guid locationId, CancellationToken cancellationToken) =>
        await db.Products.AnyAsync(x => x.Id == productId && x.HouseholdId == SystemDefaults.HouseholdId, cancellationToken) &&
        await db.Locations.AnyAsync(x => x.Id == locationId && x.HouseholdId == SystemDefaults.HouseholdId, cancellationToken);

    private static StockOperation CreateOperation(
        string idempotencyKey,
        StockMovementType type,
        decimal quantity,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            HouseholdId = SystemDefaults.HouseholdId,
            IdempotencyKey = idempotencyKey,
            Type = type,
            RequestedQuantity = quantity,
            OccurredAt = now
        };

    private static StockMovement CreateMovement(
        StockOperation operation,
        StockLot lot,
        StockMovementType type,
        decimal delta,
        string? note,
        DateTimeOffset now) =>
        new()
        {
            StockOperationId = operation.Id,
            StockLotId = lot.Id,
            ProductId = lot.ProductId,
            LocationId = lot.LocationId,
            Type = type,
            QuantityDelta = delta,
            OccurredAt = now,
            Note = NormalizeNote(note)
        };

    private static string? NormalizeNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
