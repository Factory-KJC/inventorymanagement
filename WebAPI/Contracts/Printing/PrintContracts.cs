using InventoryAPI.Domain.Printing;

namespace InventoryAPI.Contracts.Printing;

public sealed record CreatePrintJobRequest(PrintPaperWidth PaperWidth = PrintPaperWidth.Mm80);
public sealed record PrintPreviewResponse(PrintPaperWidth PaperWidth, string ReceiptLine);
public sealed record CompletePrintJobRequest(string? Error);
public sealed record PrintJobResponse(Guid Id, Guid ShoppingListId, PrintPaperWidth PaperWidth, string ReceiptLine,
    PrintJobStatus Status, int AttemptCount, string? LastError, DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);
