namespace InventoryAPI.Domain;

/// <summary>
/// 単一世帯版で使用する初期データの固定IDです。
/// 将来マルチテナント化する際は、認証ユーザーから世帯IDを解決する仕組みに置き換えます。
/// </summary>
public static class SystemDefaults
{
    public static readonly Guid HouseholdId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid LocationId = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
