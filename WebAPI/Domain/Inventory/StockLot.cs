namespace InventoryAPI.Domain.Inventory;

/// <summary>
/// 在庫ロットを表すエンティティ
/// </summary>
public sealed class StockLot
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid ProductId { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public decimal CurrentQuantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
