using InventoryAPI.Configuration;
using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore;

const int migrationAttempts = 10;
var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
}

builder.Services.AddHomeStockApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    const string correlationHeader = "X-Correlation-ID";
    var correlationId = context.Request.Headers[correlationHeader].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    context.Response.Headers[correlationHeader] = correlationId;
    using (app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        await next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(ServiceCollectionExtensions.WebClientCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Livenessはプロセス、readinessはDB接続を含む依存関係の状態を示します。
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (ApplicationDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapControllers();

if (builder.Configuration.GetValue("Database:AutoMigrate", false))
    await MigrateDatabaseAsync(app.Services, app.Logger, migrationAttempts);

app.Run();

/// <summary>
/// DB起動直後の一時的な接続失敗を考慮し、一定回数だけマイグレーションを再試行します。
/// 最終試行の例外は握りつぶさず、アプリケーションの起動を失敗させます。
/// </summary>
static async Task MigrateDatabaseAsync(IServiceProvider services, ILogger logger, int maxAttempts)
{
    await using var scope = services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await database.Database.MigrateAsync();
            return;
        }
        catch (Exception exception) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                exception,
                "DBマイグレーションに失敗しました。{DelaySeconds}秒後に再試行します ({Attempt}/{MaxAttempts})。",
                2,
                attempt,
                maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

public partial class Program;
