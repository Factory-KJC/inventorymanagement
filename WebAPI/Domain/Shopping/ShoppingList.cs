namespace InventoryAPI.Domain.Shopping;

/// <summary>
/// 一度の買い物単位で管理するリストです。有効なリストは世帯ごとに1件を想定します。
/// </summary>
public sealed class ShoppingList
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public ShoppingListStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<ShoppingListItem> Items { get; set; } = [];
}

public enum ShoppingListStatus
{
    Active = 1,
    Completed = 2
}
