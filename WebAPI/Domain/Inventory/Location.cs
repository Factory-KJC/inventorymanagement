namespace InventoryAPI.Domain.Inventory;

/// <summary>
/// 保管場所を表すエンティティ
/// </summary>
public sealed class Location
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
