using System.Data;
using InventoryAPI.Contracts.Inventory;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Application.Inventory;

public sealed class InventoryService(ApplicationDbContext db, TimeProvider timeProvider)
{
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

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var existing = await FindOperationAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
            return InventoryResult.Success(existing);

        if (!await db.Products.AnyAsync(x => x.Id == request.ProductId && x.HouseholdId == SystemDefaults.HouseholdId, cancellationToken))
            return InventoryResult.NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var lots = await db.StockLots
            .Where(x => x.HouseholdId == SystemDefaults.HouseholdId &&
                        x.ProductId == request.ProductId &&
                        x.CurrentQuantity > 0 &&
                        (!request.LocationId.HasValue || x.LocationId == request.LocationId.Value))
            .OrderBy(x => x.ExpiresOn == null)
            .ThenBy(x => x.ExpiresOn)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (lots.Sum(x => x.CurrentQuantity) < request.Quantity)
            return InventoryResult.InsufficientStock();

        var now = timeProvider.GetUtcNow();
        var operation = CreateOperation(idempotencyKey, StockMovementType.Consume, request.Quantity, now);
        var remaining = request.Quantity;

        foreach (var lot in lots)
        {
            if (remaining == 0)
                break;

            var consumed = Math.Min(lot.CurrentQuantity, remaining);
            lot.CurrentQuantity -= consumed;
            lot.UpdatedAt = now;
            remaining -= consumed;
            operation.Movements.Add(CreateMovement(operation, lot, StockMovementType.Consume, -consumed, request.Note, now));
        }

        db.StockOperations.Add(operation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return InventoryResult.Success(operation);
    }

    private async Task<StockOperation?> FindOperationAsync(string key, CancellationToken cancellationToken) =>
        await db.StockOperations.Include(x => x.Movements).SingleOrDefaultAsync(
            x => x.HouseholdId == SystemDefaults.HouseholdId && x.IdempotencyKey == key,
            cancellationToken);

    private async Task<bool> ProductAndLocationExistAsync(Guid productId, Guid locationId, CancellationToken cancellationToken) =>
        await db.Products.AnyAsync(x => x.Id == productId && x.HouseholdId == SystemDefaults.HouseholdId, cancellationToken) &&
        await db.Locations.AnyAsync(x => x.Id == locationId && x.HouseholdId == SystemDefaults.HouseholdId, cancellationToken);

    private static StockOperation CreateOperation(string key, StockMovementType type, decimal quantity, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        HouseholdId = SystemDefaults.HouseholdId,
        IdempotencyKey = key,
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
        DateTimeOffset now) => new()
    {
        StockOperationId = operation.Id,
        StockLotId = lot.Id,
        ProductId = lot.ProductId,
        LocationId = lot.LocationId,
        Type = type,
        QuantityDelta = delta,
        OccurredAt = now,
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
    };
}
