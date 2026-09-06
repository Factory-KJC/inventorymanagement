using InventoryAPI.Application.Shopping;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Catalog;
using InventoryAPI.Domain.Shopping;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Tests;

public sealed class ShoppingListServiceTests
{
    [Fact]
    public async Task GetPrintData_ReturnsPendingItemsInNameOrderWithAvailableUnits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var now = new DateTimeOffset(2026, 8, 29, 9, 30, 0, TimeSpan.Zero);
        var productId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId,
            HouseholdId = SystemDefaults.HouseholdId,
            Name = "洗剤",
            Unit = "本",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.ShoppingLists.Add(new ShoppingList
        {
            Id = listId,
            HouseholdId = SystemDefaults.HouseholdId,
            Status = ShoppingListStatus.Active,
            CreatedAt = now.AddDays(-1),
            Items =
            [
                CreateItem(listId, productId, "洗剤", 2, ShoppingItemStatus.Pending, now),
                CreateItem(listId, null, "アルミホイル", 1, ShoppingItemStatus.Pending, now),
                CreateItem(listId, null, "購入済み", 1, ShoppingItemStatus.Purchased, now),
                CreateItem(listId, null, "却下済み", 1, ShoppingItemStatus.Dismissed, now)
            ]
        });
        await db.SaveChangesAsync();
        var service = new ShoppingListService(db, new FixedTimeProvider(now));

        var result = await service.GetPrintDataAsync(default);

        Assert.NotNull(result);
        Assert.Equal(listId, result.ShoppingListId);
        Assert.Equal(now, result.GeneratedAt);
        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal("アルミホイル", item.Name);
                Assert.Null(item.Unit);
            },
            item =>
            {
                Assert.Equal("洗剤", item.Name);
                Assert.Equal("本", item.Unit);
            });
    }

    [Fact]
    public async Task GetPrintData_WhenActiveListDoesNotExist_ReturnsNull()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new ShoppingListService(db, TimeProvider.System);

        var result = await service.GetPrintDataAsync(default);

        Assert.Null(result);
    }

    private static ShoppingListItem CreateItem(
        Guid listId,
        Guid? productId,
        string name,
        decimal quantity,
        ShoppingItemStatus status,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            ProductId = productId,
            Name = name,
            Quantity = quantity,
            Source = productId.HasValue ? ShoppingItemSource.ReorderSuggestion : ShoppingItemSource.Manual,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
