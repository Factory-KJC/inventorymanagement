namespace HomeStock.Windows.Models;

public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LoginResponse(string Token, string RefreshToken, DateTimeOffset ExpiresAt);
public sealed record RegisterUserRequest(string Username, string Password);
public sealed record MessageResponse(string Message);
public sealed record ProductResponse(Guid Id, string Name, string? Barcode, string Unit, decimal? ReorderPoint, decimal? TargetQuantity);
public sealed record CreateProductRequest(string Name, string? Barcode, string Unit, decimal? ReorderPoint, decimal? TargetQuantity);
public sealed record UpdateProductRequest(string Name, string? Barcode, string Unit, decimal? ReorderPoint, decimal? TargetQuantity);
public sealed record DeletedProductSuggestionResponse(Guid Id, string Name, string? Barcode, string Unit);
public sealed record LocationResponse(Guid Id, string Name, int SortOrder);
public sealed record CreateLocationRequest(string Name, int SortOrder);
public sealed record UpdateLocationRequest(string Name, int SortOrder);
public sealed record DeletedLocationSuggestionResponse(Guid Id, string Name, int SortOrder);
public sealed record ReceiveStockRequest(Guid ProductId, Guid LocationId, decimal Quantity, DateOnly? ExpiresOn, string? Note);
public sealed record ConsumeStockRequest(Guid ProductId, decimal Quantity, Guid? LocationId, string? Note);
public sealed record AdjustStockRequest(Guid LotId, decimal CountedQuantity, string? Note);
public sealed record InventoryLotResponse(Guid LotId, Guid ProductId, string ProductName, string? Barcode, string Unit, Guid LocationId, string LocationName, DateOnly? ExpiresOn, decimal Quantity);
public sealed record DashboardResponse(int ProductCount, int LowStockCount, int ExpiringSoonCount, int ShoppingItemCount);

public enum ShoppingListStatus
{
    Active = 1,
    Completed = 2,
    Received = 3,
}

public enum ShoppingItemSource
{
    Manual = 1,
    ReorderSuggestion = 2,
}

public enum ShoppingItemStatus
{
    Pending = 1,
    Purchased = 2,
    Dismissed = 3,
}

public sealed record ShoppingListResponse(Guid? Id, ShoppingListStatus Status, DateTimeOffset? CreatedAt, IReadOnlyList<ShoppingItemResponse> Items);
public sealed record ShoppingItemResponse(Guid Id, Guid? ProductId, string Name, decimal Quantity, ShoppingItemSource Source, ShoppingItemStatus Status);
public sealed record AddShoppingItemRequest(Guid? ProductId, string Name, decimal Quantity);
public sealed record UpdateShoppingItemRequest(decimal? Quantity, ShoppingItemStatus? Status);

public enum PrintPaperWidth
{
    Mm58 = 58,
    Mm80 = 80,
}

public enum PrintJobStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
}

public sealed record CreatePrintJobRequest(PrintPaperWidth PaperWidth);
public sealed record PrintPreviewResponse(PrintPaperWidth PaperWidth, string ReceiptLine);
public sealed record PrintJobResponse(
    Guid Id,
    Guid ShoppingListId,
    PrintPaperWidth PaperWidth,
    string ReceiptLine,
    PrintJobStatus Status,
    int AttemptCount,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record StoredSession(Uri ApiBaseUri, string Username, string RefreshToken);

public sealed record ScanResult(string Barcode, bool IsSuccess, string Message)
{
    public static ScanResult Success(string barcode, string message) => new(barcode, true, message);
    public static ScanResult Failure(string barcode, string message) => new(barcode, false, message);
}
