namespace InventoryAPI.Domain.Inventory;


/// <summary>
/// 在庫操作を表すエンティティ
/// </summary>
public sealed class StockOperation
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public StockMovementType Type { get; set; }
    public decimal RequestedQuantity { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public List<StockMovement> Movements { get; set; } = [];
}
