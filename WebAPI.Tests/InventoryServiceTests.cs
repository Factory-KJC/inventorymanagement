using InventoryAPI.Application.Inventory;
using InventoryAPI.Contracts.Inventory;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Catalog;
using InventoryAPI.Domain.Households;
using InventoryAPI.Domain.Inventory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Tests;

public sealed class InventoryServiceTests
{
    [Fact]
    public async Task Receive_WithSameIdempotencyKey_IsAppliedOnlyOnce()
    {
        await using var fixture = await InventoryFixture.CreateAsync();
        var request = new ReceiveStockRequest(fixture.ProductId, SystemDefaults.LocationId, 5, null, null);

        var first = await fixture.Service.ReceiveAsync(request, "same-key", default);
        var second = await fixture.Service.ReceiveAsync(request, "same-key", default);

        Assert.Equal(InventoryResultStatus.Success, first.Status);
        Assert.Equal(first.Operation!.Id, second.Operation!.Id);
        Assert.Equal(5, (await fixture.Db.StockLots.ToListAsync()).Sum(x => x.CurrentQuantity));
        Assert.Equal(1, await fixture.Db.StockOperations.CountAsync());
    }

    [Fact]
    public async Task Consume_UsesLotsInEarliestExpiryOrder()
    {
        await using var fixture = await InventoryFixture.CreateAsync();
        await fixture.Service.ReceiveAsync(new(fixture.ProductId, SystemDefaults.LocationId, 3, new DateOnly(2027, 1, 1), null), "receive-late", default);
        await fixture.Service.ReceiveAsync(new(fixture.ProductId, SystemDefaults.LocationId, 2, new DateOnly(2026, 9, 1), null), "receive-early", default);
        await fixture.Service.ReceiveAsync(new(fixture.ProductId, SystemDefaults.LocationId, 4, null, null), "receive-no-expiry", default);

        var result = await fixture.Service.ConsumeAsync(new(fixture.ProductId, 4, null, "使用"), "consume", default);

        Assert.Equal(InventoryResultStatus.Success, result.Status);
        var lots = await fixture.Db.StockLots.OrderBy(x => x.ExpiresOn == null).ThenBy(x => x.ExpiresOn).ToListAsync();
        Assert.Equal(0, lots[0].CurrentQuantity);
        Assert.Equal(1, lots[1].CurrentQuantity);
        Assert.Equal(4, lots[2].CurrentQuantity);
        Assert.Equal(2, result.Operation!.Movements.Count);
        Assert.Equal(-4, result.Operation.Movements.Sum(x => x.QuantityDelta));
    }

    [Fact]
    public async Task Consume_WhenStockIsInsufficient_DoesNotChangeStock()
    {
        await using var fixture = await InventoryFixture.CreateAsync();
        await fixture.Service.ReceiveAsync(new(fixture.ProductId, SystemDefaults.LocationId, 2, null, null), "receive", default);

        var result = await fixture.Service.ConsumeAsync(new(fixture.ProductId, 3, null, null), "consume", default);

        Assert.Equal(InventoryResultStatus.InsufficientStock, result.Status);
        Assert.Equal(2, (await fixture.Db.StockLots.ToListAsync()).Sum(x => x.CurrentQuantity));
        Assert.Equal(1, await fixture.Db.StockOperations.CountAsync());
    }

    private sealed class InventoryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Db { get; }
        public InventoryService Service { get; }
        public Guid ProductId { get; }

        private InventoryFixture(SqliteConnection connection, ApplicationDbContext db, Guid productId)
        {
            this.connection = connection;
            Db = db;
            ProductId = productId;
            Service = new InventoryService(db, new FixedTimeProvider(new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero)));
        }

        public static async Task<InventoryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var productId = Guid.NewGuid();
            db.Products.Add(new Product
            {
                Id = productId,
                HouseholdId = SystemDefaults.HouseholdId,
                Name = "テスト商品",
                Unit = "個",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            return new InventoryFixture(connection, db, productId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
