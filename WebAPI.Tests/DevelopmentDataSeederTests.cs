using InventoryAPI.Application.Inventory;
using InventoryAPI.Contracts.Inventory;
using InventoryAPI.Controllers;
using InventoryAPI.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Tests;

public sealed class DevelopmentDataSeederTests
{
    [Fact]
    public async Task SeedAsync_RunTwice_CreatesEnoughDataForDisplayLimitWithoutDuplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero));

        await DevelopmentDataSeeder.SeedAsync(db, timeProvider);
        await DevelopmentDataSeeder.SeedAsync(db, timeProvider);

        Assert.Equal(DevelopmentDataSeeder.SampleRecordCount, await db.Products.CountAsync());
        Assert.Equal(DevelopmentDataSeeder.SampleRecordCount, await db.StockLots.CountAsync());
        Assert.Equal(10, await db.StockLots.CountAsync(lot => lot.ExpiresOn != null));

        var controller = new InventoryController(db, new InventoryService(db, timeProvider));
        var response = await controller.GetInventory(
            null,
            null,
            null,
            null,
            "quantity",
            "desc",
            InventoryController.DefaultLimit,
            default);
        var inventory = Assert.IsAssignableFrom<IReadOnlyList<InventoryLotResponse>>(response.Value);

        Assert.Equal(InventoryController.DefaultLimit, inventory.Count);
        Assert.Equal(inventory.OrderByDescending(item => item.Quantity).ThenBy(item => item.ProductName), inventory);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
