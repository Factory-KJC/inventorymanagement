using InventoryAPI.Data;
using InventoryAPI.Domain;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Tests;

public sealed class PostgreSqlIntegrationTests
{
    [PostgreSqlFact]
    public async Task Migrations_ApplyAndSeedRequiredData_OnPostgreSql()
    {
        var connectionString = Environment.GetEnvironmentVariable("HOMESTOCK_POSTGRES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new ApplicationDbContext(options);

        await db.Database.MigrateAsync();

        Assert.True(await db.Households.AnyAsync(household => household.Id == SystemDefaults.HouseholdId));
        Assert.True(await db.Locations.AnyAsync(location => location.Id == SystemDefaults.LocationId));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}

internal sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOMESTOCK_POSTGRES_TEST_CONNECTION")))
            Skip = "HOMESTOCK_POSTGRES_TEST_CONNECTION is not configured.";
    }
}
