namespace InventoryAPI.Domain.Printing;

/// <summary>
/// 再印刷可能な買い物リストの印刷スナップショットです。
/// 在庫処理とは独立して失敗状態と試行回数を保持します。
/// </summary>
public sealed class PrintJob
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid ShoppingListId { get; set; }
    public PrintPaperWidth PaperWidth { get; set; }
    public string ReceiptLine { get; set; } = string.Empty;
    public PrintJobStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum PrintPaperWidth { Mm58 = 58, Mm80 = 80 }
public enum PrintJobStatus { Pending = 1, Processing = 2, Succeeded = 3, Failed = 4 }
