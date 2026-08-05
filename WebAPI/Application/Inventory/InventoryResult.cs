using InventoryAPI.Domain.Inventory;

namespace InventoryAPI.Application.Inventory;

public sealed record InventoryResult(InventoryResultStatus Status, StockOperation? Operation = null)
{
    public static InventoryResult Success(StockOperation operation) => new(InventoryResultStatus.Success, operation);
    public static InventoryResult NotFound() => new(InventoryResultStatus.NotFound);
    public static InventoryResult InsufficientStock() => new(InventoryResultStatus.InsufficientStock);
}

public enum InventoryResultStatus
{
    Success,
    NotFound,
    InsufficientStock
}
