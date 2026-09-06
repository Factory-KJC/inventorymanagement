using System.Net;
using System.Net.Http;
using System.Text;
using HomeStock.Windows.Models;
using HomeStock.Windows.Services;

namespace HomeStock.Windows.Tests;

public sealed class ContinuousReceivingTests
{
    [Fact]
    public async Task ReceiveBarcodeAsync_FiftyConsecutiveScans_ReceivesEveryItemWithUniqueKey()
    {
        var handler = new RecordingHandler();
        var store = new MemoryCredentialStore();
        var client = new HomeStockApiClient(new HttpClient(handler), store);
        await client.LoginAsync(new Uri("https://example.test/"), "tester", "password", CancellationToken.None);
        var service = new ContinuousReceivingService(client);

        for (var i = 0; i < 50; i++)
        {
            var result = await service.ReceiveBarcodeAsync("4901234567894", Guid.Parse("22222222-2222-2222-2222-222222222222"), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        Assert.Equal(50, handler.IdempotencyKeys.Count);
        Assert.Equal(50, handler.IdempotencyKeys.Distinct().Count());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> IdempotencyKeys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.AbsolutePath switch
            {
                "/api/auth/login" => "{\"token\":\"access\",\"refreshToken\":\"refresh\",\"expiresAt\":\"2099-01-01T00:00:00Z\"}",
                "/api/products" => "[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"テスト商品\",\"barcode\":\"4901234567894\",\"unit\":\"個\",\"reorderPoint\":null,\"targetQuantity\":null}]",
                _ => "{}",
            };
            if (request.Headers.TryGetValues("Idempotency-Key", out var values))
            {
                IdempotencyKeys.Add(values.Single());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class MemoryCredentialStore : ICredentialStore
    {
        private StoredSession? _session;
        public StoredSession? Read() => _session;
        public void Save(StoredSession session) => _session = session;
        public void Delete() => _session = null;
    }
}
