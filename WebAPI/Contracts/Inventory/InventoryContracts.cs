using System.ComponentModel.DataAnnotations;
using InventoryAPI.Domain.Inventory;

namespace InventoryAPI.Contracts.Inventory;

public sealed record CreateLocationRequest([Required, StringLength(100)] string Name, int SortOrder = 0);
public sealed record LocationResponse(Guid Id, string Name, int SortOrder);

public sealed record ReceiveStockRequest(
    Guid ProductId,
    Guid LocationId,
    [Range(typeof(decimal), "0.0001", "999999999")] decimal Quantity,
    DateOnly? ExpiresOn,
    [StringLength(500)] string? Note);

public sealed record ConsumeStockRequest(
    Guid ProductId,
    [Range(typeof(decimal), "0.0001", "999999999")] decimal Quantity,
    Guid? LocationId,
    [StringLength(500)] string? Note);

public sealed record StockOperationResponse(
    Guid OperationId,
    StockMovementType Type,
    decimal RequestedQuantity,
    DateTimeOffset OccurredAt,
    IReadOnlyList<StockMovementResponse> Movements);

public sealed record StockMovementResponse(
    long Id,
    Guid LotId,
    Guid ProductId,
    Guid LocationId,
    StockMovementType Type,
    decimal QuantityDelta,
    DateTimeOffset OccurredAt,
    string? Note);

public sealed record InventoryLotResponse(
    Guid LotId,
    Guid ProductId,
    string ProductName,
    string? Barcode,
    string Unit,
    Guid LocationId,
    string LocationName,
    DateOnly? ExpiresOn,
    decimal Quantity);
