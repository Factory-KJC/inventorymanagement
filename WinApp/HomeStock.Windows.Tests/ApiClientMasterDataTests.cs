using System.Net;
using System.Net.Http;
using System.Text;
using HomeStock.Windows.Models;
using HomeStock.Windows.Services;

namespace HomeStock.Windows.Tests;

public sealed class ApiClientMasterDataTests
{
    [Fact]
    public async Task CreateProductAndLocationAsync_SendsAuthenticatedMasterDataRequests()
    {
        var handler = new MasterDataHandler();
        var client = new HomeStockApiClient(new HttpClient(handler), new MemoryCredentialStore());
        await client.LoginAsync(new Uri("https://example.test/"), "tester", "password", CancellationToken.None);

        var product = await client.CreateProductAsync(
            new CreateProductRequest("牛乳", "4901234567894", "本", 1, 2),
            CancellationToken.None);
        var location = await client.CreateLocationAsync(
            new CreateLocationRequest("冷蔵庫", 10),
            CancellationToken.None);
        product = await client.UpdateProductAsync(
            product.Id,
            new UpdateProductRequest("低脂肪乳", product.Barcode, "パック", 2, 4),
            CancellationToken.None);
        location = await client.UpdateLocationAsync(
            location.Id,
            new UpdateLocationRequest("冷蔵室", 20),
            CancellationToken.None);
        await client.DeleteProductAsync(product.Id, CancellationToken.None);
        await client.DeleteLocationAsync(location.Id, CancellationToken.None);
        var productSuggestion = Assert.Single(await client.GetDeletedProductSuggestionsAsync("低脂肪", CancellationToken.None));
        var locationSuggestion = Assert.Single(await client.GetDeletedLocationSuggestionsAsync("冷蔵", CancellationToken.None));
        await client.RestoreProductAsync(
            productSuggestion.Id,
            new UpdateProductRequest("低脂肪乳", product.Barcode, "パック", 2, 4),
            CancellationToken.None);
        await client.RestoreLocationAsync(
            locationSuggestion.Id,
            new UpdateLocationRequest("冷蔵室", 20),
            CancellationToken.None);

        Assert.Equal("低脂肪乳", product.Name);
        Assert.Equal("冷蔵室", location.Name);
        Assert.All(handler.MasterDataRequests, request => Assert.Equal("Bearer access", request.Authorization));
        Assert.Contains("\"barcode\":\"4901234567894\"", handler.MasterDataRequests[0].Body);
        Assert.Contains("\"sortOrder\":10", handler.MasterDataRequests[1].Body);
        Assert.Equal("PATCH", handler.MasterDataRequests[2].Method);
        Assert.Contains("\"reorderPoint\":2", handler.MasterDataRequests[2].Body);
        Assert.Equal("PATCH", handler.MasterDataRequests[3].Method);
        Assert.Contains("\"sortOrder\":20", handler.MasterDataRequests[3].Body);
    }

    private sealed class MasterDataHandler : HttpMessageHandler
    {
        public List<(string Authorization, string Method, string Body)> MasterDataRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var json = (path, request.Method.Method) switch
            {
                ("/api/auth/login", _) => "{\"token\":\"access\",\"refreshToken\":\"refresh\",\"expiresAt\":\"2099-01-01T00:00:00Z\"}",
                ("/api/products", _) => "{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"牛乳\",\"barcode\":\"4901234567894\",\"unit\":\"本\",\"reorderPoint\":1,\"targetQuantity\":2}",
                ("/api/locations", _) => "{\"id\":\"22222222-2222-2222-2222-222222222222\",\"name\":\"冷蔵庫\",\"sortOrder\":10}",
                ("/api/products/11111111-1111-1111-1111-111111111111", "PATCH") => "{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"低脂肪乳\",\"barcode\":\"4901234567894\",\"unit\":\"パック\",\"reorderPoint\":2,\"targetQuantity\":4}",
                ("/api/locations/22222222-2222-2222-2222-222222222222", "PATCH") => "{\"id\":\"22222222-2222-2222-2222-222222222222\",\"name\":\"冷蔵室\",\"sortOrder\":20}",
                ("/api/products/11111111-1111-1111-1111-111111111111", "DELETE") => "{}",
                ("/api/locations/22222222-2222-2222-2222-222222222222", "DELETE") => "{}",
                ("/api/products/deleted-suggestions", "GET") => "[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"低脂肪乳\",\"barcode\":\"4901234567894\",\"unit\":\"パック\"}]",
                ("/api/locations/deleted-suggestions", "GET") => "[{\"id\":\"22222222-2222-2222-2222-222222222222\",\"name\":\"冷蔵室\",\"sortOrder\":20}]",
                ("/api/products/11111111-1111-1111-1111-111111111111/restore", "POST") => "{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"低脂肪乳\",\"barcode\":\"4901234567894\",\"unit\":\"パック\",\"reorderPoint\":2,\"targetQuantity\":4}",
                ("/api/locations/22222222-2222-2222-2222-222222222222/restore", "POST") => "{\"id\":\"22222222-2222-2222-2222-222222222222\",\"name\":\"冷蔵室\",\"sortOrder\":20}",
                _ => throw new InvalidOperationException($"Unexpected path: {path}"),
            };
            if (path.StartsWith("/api/products", StringComparison.Ordinal) || path.StartsWith("/api/locations", StringComparison.Ordinal))
            {
                MasterDataRequests.Add((
                    request.Headers.Authorization?.ToString() ?? string.Empty,
                    request.Method.Method,
                    request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class MemoryCredentialStore : ICredentialStore
    {
        public StoredSession? Read() => null;
        public void Save(StoredSession session) { }
        public void Delete() { }
    }
}
