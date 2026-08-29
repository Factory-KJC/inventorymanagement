namespace HomeStock.Windows.Services;

/// <summary>ViewModelが必要とするユーザー確認をUI実装から分離します。</summary>
public interface IUserInteraction
{
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}
