namespace InventoryAPI.Contracts.Dashboard;

public sealed record DashboardResponse(
    int ProductCount,
    int LowStockCount,
    int ExpiringSoonCount,
    int ShoppingItemCount);

public sealed record DashboardListResponse(string Category, int Page, int PageSize, int TotalCount, IReadOnlyList<DashboardItemResponse> Items);
public sealed record DashboardItemResponse(Guid Id, string Name, string Detail, decimal? Quantity);
