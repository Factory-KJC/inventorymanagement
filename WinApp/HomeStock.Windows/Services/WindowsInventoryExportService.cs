using HomeStock.Windows.Models;
using Microsoft.Maui.Platform;
using Windows.Storage.Pickers;

namespace HomeStock.Windows.Services;

/// <summary>WinUIの保存ダイアログで選択したCSVファイルへ在庫を書き出します。</summary>
public sealed class WindowsInventoryExportService : IInventoryExportService
{
    public async Task<bool> ExportAsync(IReadOnlyCollection<InventoryLotResponse> inventory, CancellationToken cancellationToken)
    {
        var mauiWindow = Application.Current?.Windows.FirstOrDefault()
            ?? throw new InvalidOperationException("保存ダイアログを表示できるウィンドウがありません。");
        var nativeWindow = mauiWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("WinUIウィンドウを取得できません。");
        var picker = new FileSavePicker
        {
            SuggestedFileName = $"home-stock-{DateTime.Now:yyyyMMdd}",
        };
        picker.FileTypeChoices.Add("CSVファイル", [".csv"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, nativeWindow.GetWindowHandle());
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return false;
        }

        await CsvExporter.WriteAsync(file.Path, inventory, cancellationToken);
        return true;
    }
}
