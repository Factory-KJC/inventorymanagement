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
