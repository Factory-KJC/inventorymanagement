using System.Net;
using System.Net.Http;
using System.Text;
using HomeStock.Windows.Models;
using HomeStock.Windows.Services;

namespace HomeStock.Windows.Tests;

public sealed class ApiClientPwaFeatureTests
{
    [Fact]
    public async Task PwaFeatureMethods_SendExpectedAuthenticatedRequests()
    {
        var handler = new PwaFeatureHandler();
        var client = new HomeStockApiClient(new HttpClient(handler), new MemoryCredentialStore());
        await client.LoginAsync(new Uri("https://example.test/"), "tester", "password", CancellationToken.None);

        var dashboard = await client.GetDashboardAsync(CancellationToken.None);
        var shopping = await client.GetShoppingListAsync(CancellationToken.None);
        shopping = await client.AddShoppingItemAsync(new AddShoppingItemRequest(null, "洗剤", 2), CancellationToken.None);
        shopping = await client.GenerateShoppingSuggestionsAsync(CancellationToken.None);
        shopping = await client.UpdateShoppingItemAsync(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            new UpdateShoppingItemRequest(null, ShoppingItemStatus.Purchased),
            CancellationToken.None);
        var preview = await client.GetPrintPreviewAsync(PrintPaperWidth.Mm80, CancellationToken.None);
        var printJob = await client.CreatePrintJobAsync(PrintPaperWidth.Mm80, CancellationToken.None);
        await client.ConsumeAsync(
            new ConsumeStockRequest(Guid.NewGuid(), 1, null, "test"),
            "consume-key",
            CancellationToken.None);

        Assert.Equal(4, dashboard.ProductCount);
        Assert.Equal(ShoppingItemStatus.Purchased, shopping.Items[0].Status);
        Assert.Contains("買い物リスト", preview.ReceiptLine);
        Assert.Equal(PrintJobStatus.Pending, printJob.Status);
        Assert.All(handler.AuthorizedRequests, request => Assert.Equal("Bearer access", request.Authorization));
        Assert.Contains(handler.AuthorizedRequests, request => request.Method == "PATCH" && request.Body.Contains("\"status\":\"Purchased\""));
        Assert.Contains(handler.AuthorizedRequests, request => request.Path == "/api/inventory/consume" && request.IdempotencyKey == "consume-key");
        Assert.Contains(handler.AuthorizedRequests, request => request.Path == "/api/shopping-lists/current/print-jobs" && request.Body.Contains("\"paperWidth\":\"Mm80\""));
    }

    [Fact]
    public async Task RegisterInitialUserAsync_SendsSetupTokenWithoutAuthentication()
    {
        var handler = new PwaFeatureHandler();
        var client = new HomeStockApiClient(new HttpClient(handler), new MemoryCredentialStore());

        var message = await client.RegisterInitialUserAsync(
            new Uri("https://example.test/"),
            "tester",
            "very-secure-password",
            "setup-secret",
            CancellationToken.None);

        Assert.Equal("登録しました。", message);
        Assert.Equal("setup-secret", handler.SetupToken);
    }

    [Fact]
    public async Task LoginAndRegisterCanBeRepeated_AfterHttpClientHasSentRequests()
    {
        var handler = new PwaFeatureHandler();
        var client = new HomeStockApiClient(new HttpClient(handler), new MemoryCredentialStore());

        await client.LoginAsync(new Uri("https://first.example.test/"), "tester", "password", CancellationToken.None);
        await client.LoginAsync(new Uri("https://second.example.test/"), "tester", "password", CancellationToken.None);
        var message = await client.RegisterInitialUserAsync(
            new Uri("https://setup.example.test/"),
            "tester",
            "very-secure-password",
            "setup-secret",
            CancellationToken.None);

        Assert.Equal("登録しました。", message);
        Assert.Equal(
            ["first.example.test", "second.example.test"],
            handler.LoginHosts);
    }

    private sealed class PwaFeatureHandler : HttpMessageHandler
    {
        private const string ShoppingJson = "{\"id\":\"22222222-2222-2222-2222-222222222222\",\"status\":\"Active\",\"createdAt\":\"2026-08-29T00:00:00Z\",\"items\":[{\"id\":\"33333333-3333-3333-3333-333333333333\",\"productId\":null,\"name\":\"洗剤\",\"quantity\":2,\"source\":\"Manual\",\"status\":\"Purchased\"}]}";

        public List<(string Path, string Method, string Authorization, string? IdempotencyKey, string Body)> AuthorizedRequests { get; } = [];
        public List<string> LoginHosts { get; } = [];
        public string? SetupToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            if (path == "/api/users/register")
            {
                SetupToken = request.Headers.GetValues("X-Setup-Token").Single();
                return Json("{\"message\":\"登録しました。\"}");
            }

            if (path == "/api/auth/login")
            {
                LoginHosts.Add(request.RequestUri.Host);
                return Json("{\"token\":\"access\",\"refreshToken\":\"refresh\",\"expiresAt\":\"2099-01-01T00:00:00Z\"}");
            }

            AuthorizedRequests.Add((
                path,
                request.Method.Method,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null,
                body));

            return path switch
            {
                "/api/dashboard" => Json("{\"productCount\":4,\"lowStockCount\":1,\"expiringSoonCount\":2,\"shoppingItemCount\":1}"),
                "/api/shopping-lists/current" => Json(ShoppingJson),
                "/api/shopping-lists/current/items" => Json(ShoppingJson),
                "/api/shopping-lists/current/generate" => Json(ShoppingJson),
                "/api/shopping-lists/current/items/33333333-3333-3333-3333-333333333333" => Json(ShoppingJson),
                "/api/shopping-lists/current/print-preview" => Json("{\"paperWidth\":\"Mm80\",\"receiptLine\":\"-\\n^^^買い物リスト^^^\\n-\"}"),
                "/api/shopping-lists/current/print-jobs" => Json("{\"id\":\"44444444-4444-4444-4444-444444444444\",\"shoppingListId\":\"22222222-2222-2222-2222-222222222222\",\"paperWidth\":\"Mm80\",\"receiptLine\":\"preview\",\"status\":\"Pending\",\"attemptCount\":0,\"lastError\":null,\"createdAt\":\"2026-08-30T00:00:00Z\",\"startedAt\":null,\"completedAt\":null}"),
                "/api/inventory/consume" => Json("{}"),
                _ => throw new InvalidOperationException($"Unexpected path: {path}"),
            };
        }

        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class MemoryCredentialStore : ICredentialStore
    {
        public StoredSession? Read() => null;
        public void Save(StoredSession session) { }
        public void Delete() { }
    }
}
