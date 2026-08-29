namespace InventoryAPI.Models;

/// <summary>
/// ローテーション可能な更新トークンの失効状態を保持します。トークン本体は保存しません。
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}
