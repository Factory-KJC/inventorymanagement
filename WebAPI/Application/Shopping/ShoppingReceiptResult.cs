using InventoryAPI.Domain.Inventory;

namespace InventoryAPI.Application.Shopping;

/// <summary>
/// 完了した買い物リストを入庫へ変換した結果を表します。
/// </summary>
public sealed record ShoppingReceiptResult(
    ShoppingReceiptResultStatus Status,
    StockOperation? Operation = null)
{
    public static ShoppingReceiptResult Success(StockOperation operation) =>
        new(ShoppingReceiptResultStatus.Success, operation);

    public static ShoppingReceiptResult NotFound() => new(ShoppingReceiptResultStatus.NotFound);

    public static ShoppingReceiptResult InvalidState() => new(ShoppingReceiptResultStatus.InvalidState);

    public static ShoppingReceiptResult InvalidItems() => new(ShoppingReceiptResultStatus.InvalidItems);
}

public enum ShoppingReceiptResultStatus
{
    Success,
    NotFound,
    InvalidState,
    InvalidItems
}
