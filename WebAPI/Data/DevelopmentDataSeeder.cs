using InventoryAPI.Domain;
using InventoryAPI.Domain.Catalog;
using InventoryAPI.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Data;

/// <summary>
/// 開発環境で一覧表示や検索を確認するための再実行可能なサンプルデータを投入します。
/// </summary>
public static class DevelopmentDataSeeder
{
    public const int SampleRecordCount = 30;
    private static readonly Guid ProductIdNamespace = Guid.Parse("7d67a9e4-1cf8-4a0a-9000-000000000000");
    private static readonly Guid StockLotIdNamespace = Guid.Parse("9d3e08d9-8d7a-4c5d-9000-000000000000");

    /// <summary>
    /// 固定IDを使い、未投入のサンプル商品と在庫ロットだけを追加します。
    /// </summary>
    public static async Task SeedAsync(ApplicationDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken = default)
    {
        var productIds = Enumerable.Range(1, SampleRecordCount).Select(index => CreateId(ProductIdNamespace, index)).ToArray();
        var existingProductIds = (await db.Products
            .Where(product => productIds.Contains(product.Id))
            .Select(product => product.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        var now = timeProvider.GetUtcNow();

        for (var index = 1; index <= SampleRecordCount; index++)
        {
            var productId = CreateId(ProductIdNamespace, index);
            if (!existingProductIds.Contains(productId))
            {
                db.Products.Add(new Product
                {
                    Id = productId,
                    HouseholdId = SystemDefaults.HouseholdId,
                    Name = $"サンプル商品{index:00}",
                    Unit = index % 3 == 0 ? "本" : "個",
                    ReorderPoint = 2,
                    TargetQuantity = 5,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var lotIds = Enumerable.Range(1, SampleRecordCount).Select(index => CreateId(StockLotIdNamespace, index)).ToArray();
        var existingLotIds = (await db.StockLots
            .Where(lot => lotIds.Contains(lot.Id))
            .Select(lot => lot.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        for (var index = 1; index <= SampleRecordCount; index++)
        {
            var lotId = CreateId(StockLotIdNamespace, index);
            if (existingLotIds.Contains(lotId))
            {
                continue;
            }

            db.StockLots.Add(new StockLot
            {
                Id = lotId,
                HouseholdId = SystemDefaults.HouseholdId,
                ProductId = CreateId(ProductIdNamespace, index),
                LocationId = SystemDefaults.LocationId,
                ExpiresOn = index <= 10 ? today.AddDays(index % 7 + 1) : null,
                CurrentQuantity = index % 5 + 1,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid CreateId(Guid idNamespace, int index)
    {
        var bytes = idNamespace.ToByteArray();
        BitConverter.GetBytes(index).CopyTo(bytes, 12);
        return new Guid(bytes);
    }
}
