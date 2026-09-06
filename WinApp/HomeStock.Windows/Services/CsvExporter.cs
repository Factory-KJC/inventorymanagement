using System.Globalization;
using System.IO;
using System.Text;
using HomeStock.Windows.Models;

namespace HomeStock.Windows.Services;

public static class CsvExporter
{
    public static async Task WriteAsync(string path, IEnumerable<InventoryLotResponse> lots, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        await writer.WriteLineAsync("商品名,JANコード,保管場所,期限,数量,単位");
        foreach (var lot in lots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = string.Join(',', new[]
            {
                Escape(lot.ProductName), Escape(lot.Barcode), Escape(lot.LocationName),
                Escape(lot.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                lot.Quantity.ToString(CultureInfo.InvariantCulture), Escape(lot.Unit),
            });
            await writer.WriteLineAsync(row);
        }
    }

    private static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}
