using InventoryAPI.Domain;
using InventoryAPI.Domain.Catalog;
using InventoryAPI.Domain.Households;
using InventoryAPI.Domain.Inventory;
using InventoryAPI.Domain.Printing;
using InventoryAPI.Domain.Shopping;
using InventoryAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryAPI.Data.Configurations;

internal static class DatabaseSchema
{
    public const string Inventory = "inventorymanagement";
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("users", DatabaseSchema.Inventory);
        // 既存DBとの互換性を保ちながら、C#側では標準的な命名を使用します。
        entity.Property(user => user.PasswordHash).HasColumnName("Password_Hash");
        entity.HasIndex(user => user.Username).IsUnique();
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("refresh_tokens", DatabaseSchema.Inventory);
        entity.Property(token => token.TokenHash).HasMaxLength(64);
        entity.HasIndex(token => token.TokenHash).IsUnique();
        entity.HasOne(token => token.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> entity)
    {
        entity.ToTable("households", DatabaseSchema.Inventory);
        entity.Property(household => household.Name).HasMaxLength(100);
        entity.Property(household => household.TimeZone).HasMaxLength(100);
        entity.HasData(new Household
        {
            Id = SystemDefaults.HouseholdId,
            Name = "自宅",
            TimeZone = "Asia/Tokyo"
        });
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> entity)
    {
        entity.ToTable("products", DatabaseSchema.Inventory);
        entity.Property(product => product.Name).HasMaxLength(200);
        entity.Property(product => product.Barcode).HasMaxLength(32);
        entity.Property(product => product.Unit).HasMaxLength(20);
        entity.Property(product => product.ReorderPoint).HasPrecision(18, 4);
        entity.Property(product => product.TargetQuantity).HasPrecision(18, 4);
        entity.HasIndex(product => new { product.HouseholdId, product.Barcode }).IsUnique();
        entity.HasOne<Household>()
            .WithMany()
            .HasForeignKey(product => product.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> entity)
    {
        entity.ToTable("locations", DatabaseSchema.Inventory);
        entity.Property(location => location.Name).HasMaxLength(100);
        entity.HasIndex(location => new { location.HouseholdId, location.Name }).IsUnique();
        entity.HasOne<Household>()
            .WithMany()
            .HasForeignKey(location => location.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasData(new Location
        {
            Id = SystemDefaults.LocationId,
            HouseholdId = SystemDefaults.HouseholdId,
            Name = "未設定",
            SortOrder = 0
        });
    }
}

internal sealed class StockLotConfiguration : IEntityTypeConfiguration<StockLot>
{
    public void Configure(EntityTypeBuilder<StockLot> entity)
    {
        entity.ToTable("stock_lots", DatabaseSchema.Inventory);
        entity.Property(lot => lot.CurrentQuantity).HasPrecision(18, 4);
        entity.HasIndex(lot => new { lot.HouseholdId, lot.ProductId, lot.LocationId, lot.ExpiresOn });
        entity.HasOne<Household>()
            .WithMany()
            .HasForeignKey(lot => lot.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Product>()
            .WithMany()
            .HasForeignKey(lot => lot.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Location>()
            .WithMany()
            .HasForeignKey(lot => lot.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockOperationConfiguration : IEntityTypeConfiguration<StockOperation>
{
    public void Configure(EntityTypeBuilder<StockOperation> entity)
    {
        entity.ToTable("stock_operations", DatabaseSchema.Inventory);
        entity.Property(operation => operation.IdempotencyKey).HasMaxLength(100);
        entity.Property(operation => operation.Type).HasConversion<string>().HasMaxLength(20);
        entity.Property(operation => operation.RequestedQuantity).HasPrecision(18, 4);
        entity.HasIndex(operation => new { operation.HouseholdId, operation.IdempotencyKey }).IsUnique();
        entity.HasOne<Household>()
            .WithMany()
            .HasForeignKey(operation => operation.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> entity)
    {
        entity.ToTable("stock_movements", DatabaseSchema.Inventory);
        entity.Property(movement => movement.Type).HasConversion<string>().HasMaxLength(20);
        entity.Property(movement => movement.QuantityDelta).HasPrecision(18, 4);
        entity.Property(movement => movement.Note).HasMaxLength(500);
        entity.HasIndex(movement => new { movement.ProductId, movement.OccurredAt });
        entity.HasIndex(movement => movement.ReversesMovementId).IsUnique();
        entity.HasOne<StockOperation>()
            .WithMany(operation => operation.Movements)
            .HasForeignKey(movement => movement.StockOperationId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<StockLot>()
            .WithMany()
            .HasForeignKey(movement => movement.StockLotId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<StockMovement>()
            .WithMany()
            .HasForeignKey(movement => movement.ReversesMovementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShoppingListConfiguration : IEntityTypeConfiguration<ShoppingList>
{
    public void Configure(EntityTypeBuilder<ShoppingList> entity)
    {
        entity.ToTable("shopping_lists", DatabaseSchema.Inventory);
        entity.Property(list => list.Status).HasConversion<string>().HasMaxLength(20);
        entity.HasIndex(list => new { list.HouseholdId, list.Status });
        entity.HasOne<Household>()
            .WithMany()
            .HasForeignKey(list => list.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShoppingListItemConfiguration : IEntityTypeConfiguration<ShoppingListItem>
{
    public void Configure(EntityTypeBuilder<ShoppingListItem> entity)
    {
        entity.ToTable("shopping_list_items", DatabaseSchema.Inventory);
        entity.Property(item => item.Name).HasMaxLength(200);
        entity.Property(item => item.Quantity).HasPrecision(18, 4);
        entity.Property(item => item.Source).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        entity.HasIndex(item => new { item.ShoppingListId, item.Status });
        entity.HasOne<ShoppingList>()
            .WithMany(list => list.Items)
            .HasForeignKey(item => item.ShoppingListId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> entity)
    {
        entity.ToTable("print_jobs", DatabaseSchema.Inventory);
        entity.Property(job => job.PaperWidth).HasConversion<int>();
        entity.Property(job => job.Status).HasConversion<string>().HasMaxLength(20);
        entity.Property(job => job.ReceiptLine).HasMaxLength(20000);
        entity.Property(job => job.LastError).HasMaxLength(1000);
        entity.HasIndex(job => new { job.Status, job.CreatedAt });
        entity.HasOne<Household>().WithMany().HasForeignKey(job => job.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ShoppingList>().WithMany().HasForeignKey(job => job.ShoppingListId).OnDelete(DeleteBehavior.Restrict);
    }
}
