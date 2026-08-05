namespace InventoryAPI.Contracts.Dashboard;

/// <summary>
/// ダッシュボード上部に表示する集計値です。
/// </summary>
public sealed record DashboardResponse(
    int ProductCount,
    int LowStockCount,
    int ExpiringSoonCount,
    int ShoppingItemCount);

/// <summary>
/// ダッシュボードの集計カードから開くページング済み一覧です。
/// </summary>
public sealed record DashboardListResponse(
    string Category,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<DashboardItemResponse> Items);

public sealed record DashboardItemResponse(
    Guid Id,
    string Name,
    string Detail,
    decimal? Quantity);
