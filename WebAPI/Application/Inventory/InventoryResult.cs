using InventoryAPI.Domain.Inventory;

namespace InventoryAPI.Application.Inventory;

/// <summary>
/// 在庫サービスの結果をHTTPに依存せず表現します。
/// </summary>
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
