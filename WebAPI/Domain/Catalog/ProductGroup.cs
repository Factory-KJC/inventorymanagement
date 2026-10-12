namespace InventoryAPI.Domain.Catalog;

/// <summary>
/// ブランドやパッケージが異なっても代替できる商品を、補充判定の単位としてまとめます。
/// </summary>
public sealed class ProductGroup
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "個";
    public decimal? ReorderPoint { get; set; }
    public decimal? TargetQuantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
