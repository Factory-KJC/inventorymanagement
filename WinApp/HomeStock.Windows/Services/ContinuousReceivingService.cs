using HomeStock.Windows.Models;
using System.Net.Http;

namespace HomeStock.Windows.Services;

/// <summary>スキャンを直列化し、各読取を固有の冪等性キーで一度だけ入庫します。</summary>
public sealed class ContinuousReceivingService(HomeStockApiClient apiClient)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ScanResult> ReceiveBarcodeAsync(string barcode, Guid locationId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var products = await apiClient.GetProductsAsync(null, barcode, cancellationToken);
            var product = products.SingleOrDefault();
            if (product is null)
            {
                return ScanResult.Failure(barcode, "未登録のJANコードです。");
            }

            await apiClient.ReceiveAsync(
                new ReceiveStockRequest(product.Id, locationId, 1, null, "Windows連続スキャン"),
                $"maui-scan-{Guid.NewGuid():N}",
                cancellationToken);
            return ScanResult.Success(barcode, $"{product.Name} を1 {product.Unit}入庫しました。");
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            return ScanResult.Failure(barcode, exception.Message);
        }
        finally
        {
            _gate.Release();
        }
    }
}
