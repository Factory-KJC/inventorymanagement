using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using InventoryAPI.Contracts.Catalog;
using InventoryAPI.Contracts.Dashboard;
using InventoryAPI.Contracts.Inventory;
using InventoryAPI.Contracts.Shopping;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryAPI.Tests;

public sealed class ApiWorkflowTests
{
    [Fact]
    public async Task UpdateProduct_UpdatesFieldsAndRejectsDuplicateBarcode()
    {
        await using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);

        var firstResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            "更新前", "4901234567894", "個", 1, 3));
        var first = await firstResponse.Content.ReadFromJsonAsync<ProductResponse>();
        var secondResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            "別商品", "4901234567887", "本", null, null));
        var second = await secondResponse.Content.ReadFromJsonAsync<ProductResponse>();

        var updateResponse = await client.PatchAsJsonAsync($"/api/products/{first!.Id}", new UpdateProductRequest(
            " 更新後 ", null, " 袋 ", 2, 5));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(updated);
        Assert.Equal("更新後", updated.Name);
        Assert.Null(updated.Barcode);
        Assert.Equal("袋", updated.Unit);
        Assert.Equal(2, updated.ReorderPoint);
        Assert.Equal(5, updated.TargetQuantity);

        var duplicateResponse = await client.PatchAsJsonAsync($"/api/products/{first.Id}", new UpdateProductRequest(
            "変更されない", second!.Barcode, "箱", 1, 1));

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var unchanged = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{first.Id}");
        Assert.NotNull(unchanged);
        Assert.Equal(updated, unchanged);
    }

    [Fact]
    public async Task DiscardAndAdjust_WorkThroughHttpApiAndAreIdempotent()
    {
        await using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);

        var createProduct = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            "棚卸商品", null, "個", null, null));
        var product = await createProduct.Content.ReadFromJsonAsync<ProductResponse>();
        var location = Assert.Single((await client.GetFromJsonAsync<List<LocationResponse>>("/api/locations"))!);

        var receive = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/receive")
        {
            Content = JsonContent.Create(new ReceiveStockRequest(product!.Id, location.Id, 5, null, null))
        };
        receive.Headers.Add("Idempotency-Key", "receive-for-adjust");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(receive)).StatusCode);

        async Task<HttpResponseMessage> SendDiscardAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/discard")
            {
                Content = JsonContent.Create(new DiscardStockRequest(product.Id, 2, location.Id, "破損"))
            };
            request.Headers.Add("Idempotency-Key", "discard-same-key");
            return await client.SendAsync(request);
        }

        Assert.Equal(HttpStatusCode.OK, (await SendDiscardAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendDiscardAsync()).StatusCode);
        var inventory = await client.GetFromJsonAsync<List<InventoryLotResponse>>($"/api/inventory?productId={product.Id}");
        var lot = Assert.Single(inventory!);
        Assert.Equal(3, lot.Quantity);

        var adjust = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/adjust")
        {
            Content = JsonContent.Create(new AdjustStockRequest(lot.LotId, 4, "実数"))
        };
        adjust.Headers.Add("Idempotency-Key", "adjust-once");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(adjust)).StatusCode);

        inventory = await client.GetFromJsonAsync<List<InventoryLotResponse>>($"/api/inventory?productId={product.Id}");
        Assert.Equal(4, Assert.Single(inventory!).Quantity);
    }

    [Fact]
    public async Task DashboardList_LimitsEachPageToTwentyItems()
    {
        await using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);

        for (var index = 1; index <= 25; index++)
        {
            var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
                $"ページング商品{index:D2}", null, "個", null, null));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var firstPage = await client.GetFromJsonAsync<DashboardListResponse>(
            "/api/dashboard/products?page=1&pageSize=100");
        Assert.NotNull(firstPage);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(20, firstPage.PageSize);
        Assert.Equal(25, firstPage.TotalCount);
        Assert.Equal(20, firstPage.Items.Count);

        var secondPage = await client.GetFromJsonAsync<DashboardListResponse>(
            "/api/dashboard/products?page=2&pageSize=20");
        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(5, secondPage.Items.Count);
    }

    [Fact]
    public async Task RegisterReceiveConsumeAndReadHistory_WorksThroughHttpApi()
    {
        await using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();
        var home = await client.GetStringAsync("/");
        Assert.Contains("<title>Home Stock</title>", home);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);

        var createProduct = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            "テスト洗剤", "4901234567894", "個", 1, 3));
        Assert.Equal(HttpStatusCode.Created, createProduct.StatusCode);
        var product = await createProduct.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);

        var locations = await client.GetFromJsonAsync<List<LocationResponse>>("/api/locations");
        Assert.NotNull(locations);
        var location = Assert.Single(locations);

        var receiveRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/receive")
        {
            Content = JsonContent.Create(new ReceiveStockRequest(product.Id, location.Id, 5, new DateOnly(2027, 1, 31), "入庫"))
        };
        receiveRequest.Headers.Add("Idempotency-Key", "api-receive-001");
        var receive = await client.SendAsync(receiveRequest);
        Assert.Equal(HttpStatusCode.OK, receive.StatusCode);

        var consumeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/consume")
        {
            Content = JsonContent.Create(new ConsumeStockRequest(product.Id, 2, null, "消費"))
        };
        consumeRequest.Headers.Add("Idempotency-Key", "api-consume-001");
        var consume = await client.SendAsync(consumeRequest);
        Assert.Equal(HttpStatusCode.OK, consume.StatusCode);

        var inventory = await client.GetFromJsonAsync<List<InventoryLotResponse>>($"/api/inventory?productId={product.Id}");
        Assert.Equal(3, Assert.Single(inventory!).Quantity);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        var movements = await client.GetFromJsonAsync<List<StockMovementResponse>>(
            $"/api/movements?productId={product.Id}", jsonOptions);
        Assert.Equal(2, movements!.Count);

        var consumeRest = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/consume")
        {
            Content = JsonContent.Create(new ConsumeStockRequest(product.Id, 3, null, "使い切り"))
        };
        consumeRest.Headers.Add("Idempotency-Key", "api-consume-002");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(consumeRest)).StatusCode);

        var generated = await client.PostAsync("/api/shopping-lists/current/generate", null);
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        var shoppingList = await generated.Content.ReadFromJsonAsync<ShoppingListResponse>(jsonOptions);
        var suggested = Assert.Single(shoppingList!.Items);
        Assert.Equal(product.Id, suggested.ProductId);
        Assert.Equal(3, suggested.Quantity);
    }

    [Fact]
    public async Task CompletedShoppingList_ReceivesPurchasedItemsExactlyOnce()
    {
        await using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());

        var productResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            "購入商品", null, "個", 1, 4));
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();
        var location = Assert.Single((await client.GetFromJsonAsync<List<LocationResponse>>("/api/locations"))!);

        var generatedResponse = await client.PostAsync("/api/shopping-lists/current/generate", null);
        var generated = await generatedResponse.Content.ReadFromJsonAsync<ShoppingListResponse>(jsonOptions);
        var item = Assert.Single(generated!.Items);
        var purchasedResponse = await client.PatchAsJsonAsync(
            $"/api/shopping-lists/current/items/{item.Id}",
            new UpdateShoppingItemRequest(3, Domain.Shopping.ShoppingItemStatus.Purchased));
        Assert.Equal(HttpStatusCode.OK, purchasedResponse.StatusCode);

        var completedResponse = await client.PostAsync("/api/shopping-lists/current/complete", null);
        var completed = await completedResponse.Content.ReadFromJsonAsync<ShoppingListResponse>(jsonOptions);
        Assert.Equal(Domain.Shopping.ShoppingListStatus.Completed, completed!.Status);

        async Task<HttpResponseMessage> ReceiveAsync()
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/shopping-lists/{completed.Id}/receive")
            {
                Content = JsonContent.Create(new ReceiveShoppingListRequest([
                    new ReceiveShoppingItemRequest(item.Id, location.Id, new DateOnly(2027, 2, 28))
                ]))
            };
            request.Headers.Add("Idempotency-Key", "shopping-receive-once");
            return await client.SendAsync(request);
        }

        var firstReceive = await ReceiveAsync();
        Assert.Equal(HttpStatusCode.OK, firstReceive.StatusCode);
        var receipt = await firstReceive.Content.ReadFromJsonAsync<ReceiveShoppingListResponse>(jsonOptions);
        Assert.Equal(Domain.Shopping.ShoppingListStatus.Received, receipt!.Status);
        Assert.Equal(3, receipt.Operation.RequestedQuantity);
        Assert.Single(receipt.Operation.Movements);

        Assert.Equal(HttpStatusCode.OK, (await ReceiveAsync()).StatusCode);
        var inventory = await client.GetFromJsonAsync<List<InventoryLotResponse>>(
            $"/api/inventory?productId={product!.Id}");
        var lot = Assert.Single(inventory!);
        Assert.Equal(3, lot.Quantity);
        Assert.Equal(new DateOnly(2027, 2, 28), lot.ExpiresOn);

        var differentKeyRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/shopping-lists/{completed.Id}/receive")
        {
            Content = JsonContent.Create(new ReceiveShoppingListRequest([
                new ReceiveShoppingItemRequest(item.Id, location.Id, null)
            ]))
        };
        differentKeyRequest.Headers.Add("Idempotency-Key", "shopping-receive-again");
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(differentKeyRequest)).StatusCode);
    }

    private sealed class InventoryApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            connection.Open();
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused");
            builder.UseSetting("Jwt:Issuer", "tests");
            builder.UseSetting("Jwt:Audience", "tests");
            builder.UseSetting("Jwt:Key", "test-jwt-key-at-least-32-bytes-long");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
