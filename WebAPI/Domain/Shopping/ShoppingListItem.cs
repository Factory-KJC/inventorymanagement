namespace InventoryAPI.Domain.Shopping;

public sealed class ShoppingListItem
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public ShoppingItemSource Source { get; set; }
    public ShoppingItemStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum ShoppingItemSource
{
    Manual = 1,
    ReorderSuggestion = 2
}

public enum ShoppingItemStatus
{
    Pending = 1,
    Purchased = 2,
    Dismissed = 3
}
