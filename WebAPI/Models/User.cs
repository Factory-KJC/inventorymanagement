namespace InventoryAPI.Models;

/// <summary>
/// APIへログインできるユーザーを表します。
/// </summary>
public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// BCryptで生成したパスワードハッシュです。平文パスワードは保存しません。
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
}
