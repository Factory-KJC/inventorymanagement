using System.Net;
using System.Net.Http.Json;
using InventoryAPI.Contracts.Auth;
using InventoryAPI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace InventoryAPI.Tests;

public sealed class AuthSecurityTests
{
    private const string SetupToken = "test-setup-token-at-least-32-bytes";

    [Fact]
    public async Task Register_RequiresSetupTokenForNonLocalRequest()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var denied = await client.PostAsJsonAsync(
            "/api/users/register",
            new RegisterUserRequest("admin", "long-test-password"));
        client.DefaultRequestHeaders.Add("X-Setup-Token", SetupToken);
        var allowed = await client.PostAsJsonAsync(
            "/api/users/register",
            new RegisterUserRequest("admin", "long-test-password"));

        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesTokenAndRejectsReuse()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Setup-Token", SetupToken);
        await client.PostAsJsonAsync(
            "/api/users/register",
            new RegisterUserRequest("admin", "long-test-password"));
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin", "long-test-password"));
        var original = await login.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshed = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(original!.RefreshToken));
        var reused = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(original.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        var replacement = await refreshed.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotEqual(original.RefreshToken, replacement!.RefreshToken);
    }

    [Fact]
    public async Task Login_RejectsRequestsBeyondRateLimit()
    {
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("unknown", "wrong-password"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("unknown", "wrong-password"));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    private sealed class AuthApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
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
            builder.UseSetting("Setup:Token", SetupToken);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));

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
}
