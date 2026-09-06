namespace InventoryAPI.Domain.Inventory;


/// <summary>
/// 在庫移動を表すエンティティ
/// </summary>
public sealed class StockMovement
{
    public long Id { get; set; }
    public Guid StockOperationId { get; set; }
    public Guid StockLotId { get; set; }
    public long? ReversesMovementId { get; set; }
    public Guid ProductId { get; set; }
    public Guid LocationId { get; set; }
    public StockMovementType Type { get; set; }
    public decimal QuantityDelta { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? Note { get; set; }
}
