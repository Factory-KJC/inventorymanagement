namespace InventoryAPI.Domain.Catalog;

/// <summary>
/// 商品を表すエンティティ
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Unit { get; set; } = "個";
    public decimal? ReorderPoint { get; set; }
    public decimal? TargetQuantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
