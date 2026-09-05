using InventoryAPI.Application.Printing;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Printing;
using InventoryAPI.Domain.Shopping;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Tests;

public sealed class PrintJobServiceTests
{
    [Fact]
    public async Task CreateAsync_SnapshotsPendingItemsAsReceiptLineDocument()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var now = new DateTimeOffset(2026, 8, 30, 1, 2, 0, TimeSpan.Zero);
        var listId = Guid.NewGuid();
        db.ShoppingLists.Add(new ShoppingList
        {
            Id = listId,
            HouseholdId = SystemDefaults.HouseholdId,
            Status = ShoppingListStatus.Active,
            CreatedAt = now,
            Items =
            [
                Item(listId, "洗剤", ShoppingItemStatus.Pending, now),
                Item(listId, "購入済み", ShoppingItemStatus.Purchased, now)
            ]
        });
        await db.SaveChangesAsync();
        var service = new PrintJobService(
            db,
            new FixedTimeProvider(now, TimeZoneInfo.CreateCustomTimeZone("Server time", TimeSpan.FromHours(9), "Server time", "Server time")));

        var result = await service.CreateAsync(PrintPaperWidth.Mm80, default);

        Assert.NotNull(result);
        Assert.Equal(PrintJobStatus.Pending, result.Status);
        Assert.Equal(
            "^^^買い物リスト^^^\n-\n2026/08/30（日） 10:02\n-\n洗剤 | 2\n-\n^Home Stock^",
            result.ReceiptLine);
        Assert.Equal(result.ReceiptLine, (await db.PrintJobs.SingleAsync()).ReceiptLine);
    }

    [Fact]
    public async Task GetPreviewAsync_DoesNotCreatePrintJob()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.Parse("2026-08-30T00:00:00Z");
        var listId = Guid.NewGuid();
        db.ShoppingLists.Add(new ShoppingList
        {
            Id = listId,
            HouseholdId = SystemDefaults.HouseholdId,
            Status = ShoppingListStatus.Active,
            CreatedAt = now,
            Items = [Item(listId, "洗剤", ShoppingItemStatus.Pending, now)]
        });
        await db.SaveChangesAsync();
        var service = new PrintJobService(db, new FixedTimeProvider(now));

        var result = await service.GetPreviewAsync(PrintPaperWidth.Mm58, default);

        Assert.NotNull(result);
        Assert.Equal(PrintPaperWidth.Mm58, result.PaperWidth);
        Assert.Contains("洗剤", result.ReceiptLine);
        Assert.Empty(db.PrintJobs);
    }

    [Fact]
    public async Task RetryAsync_PreservesReceiptLineSnapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.Parse("2026-08-30T00:00:00Z");
        var list = new ShoppingList { Id = Guid.NewGuid(), HouseholdId = SystemDefaults.HouseholdId, Status = ShoppingListStatus.Completed, CreatedAt = now };
        db.ShoppingLists.Add(list);
        var job = new PrintJob { Id = Guid.NewGuid(), HouseholdId = SystemDefaults.HouseholdId, ShoppingListId = list.Id, PaperWidth = PrintPaperWidth.Mm80, ReceiptLine = "original", Status = PrintJobStatus.Failed, LastError = "paper out", CreatedAt = now };
        db.PrintJobs.Add(job);
        await db.SaveChangesAsync();
        var service = new PrintJobService(db, TimeProvider.System);

        var result = await service.RetryAsync(job.Id, default);

        Assert.NotNull(result);
        Assert.Equal("original", result.ReceiptLine);
        Assert.Equal(PrintJobStatus.Pending, result.Status);
        Assert.Null(result.LastError);
    }

    [Fact]
    public async Task RetryAsync_WhenAttemptLimitReached_DoesNotRequeueJob()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.Parse("2026-08-30T00:00:00Z");
        var list = new ShoppingList
        {
            Id = Guid.NewGuid(),
            HouseholdId = SystemDefaults.HouseholdId,
            Status = ShoppingListStatus.Completed,
            CreatedAt = now
        };
        db.ShoppingLists.Add(list);
        var job = new PrintJob
        {
            Id = Guid.NewGuid(),
            HouseholdId = SystemDefaults.HouseholdId,
            ShoppingListId = list.Id,
            PaperWidth = PrintPaperWidth.Mm80,
            ReceiptLine = "original",
            Status = PrintJobStatus.Failed,
            AttemptCount = PrintJobService.MaxAttempts,
            LastError = "paper out",
            CreatedAt = now
        };
        db.PrintJobs.Add(job);
        await db.SaveChangesAsync();
        var service = new PrintJobService(db, TimeProvider.System);

        var result = await service.RetryAsync(job.Id, default);

        Assert.Null(result);
        Assert.Equal(PrintJobStatus.Failed, job.Status);
        Assert.Equal("paper out", job.LastError);
    }

    private static ShoppingListItem Item(Guid listId, string name, ShoppingItemStatus status, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ShoppingListId = listId,
        Name = name,
        Quantity = 2,
        Source = ShoppingItemSource.Manual,
        Status = status,
        CreatedAt = now,
        UpdatedAt = now
    };

    private sealed class FixedTimeProvider(DateTimeOffset now, TimeZoneInfo? localTimeZone = null) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone { get; } = localTimeZone ?? TimeZoneInfo.Utc;
    }
}
