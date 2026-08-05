namespace InventoryAPI.Domain.Households;

/// <summary>
/// 世帯を表すエンティティ
/// </summary>
public sealed class Household
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "Asia/Tokyo";
}
