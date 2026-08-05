namespace InventoryAPI.Domain.Shopping;

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
