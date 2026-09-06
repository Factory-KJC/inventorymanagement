namespace HomeStock.Windows.Services;

/// <summary>MAUIページ上に確認ダイアログを表示します。</summary>
public sealed class MauiUserInteraction : IUserInteraction
{
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page
            ?? throw new InvalidOperationException("確認ダイアログを表示できるウィンドウがありません。");
        return page.DisplayAlertAsync(title, message, accept, cancel);
    }
}
