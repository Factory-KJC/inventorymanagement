using HomeStock.Windows.Models;

namespace HomeStock.Windows.Services;

/// <summary>表示中の在庫をユーザーが選んだファイルへ出力します。</summary>
public interface IInventoryExportService
{
    Task<bool> ExportAsync(IReadOnlyCollection<InventoryLotResponse> inventory, CancellationToken cancellationToken);
}
