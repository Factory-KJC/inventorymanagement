using InventoryAPI.Domain;
using InventoryAPI.Domain.Catalog;
using InventoryAPI.Domain.Households;
using InventoryAPI.Domain.Inventory;
using InventoryAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<StockOperation> StockOperations => Set<StockOperation>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const string schema = "inventorymanagement";

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", schema);
            entity.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<Household>(entity =>
        {
            entity.ToTable("households", schema);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.TimeZone).HasMaxLength(100);
            entity.HasData(new Household
            {
                Id = SystemDefaults.HouseholdId,
                Name = "自宅",
                TimeZone = "Asia/Tokyo"
            });
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products", schema);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Barcode).HasMaxLength(32);
            entity.Property(x => x.Unit).HasMaxLength(20);
            entity.Property(x => x.ReorderPoint).HasPrecision(18, 4);
            entity.Property(x => x.TargetQuantity).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.HouseholdId, x.Barcode }).IsUnique();
            entity.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations", schema);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.HasIndex(x => new { x.HouseholdId, x.Name }).IsUnique();
            entity.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            entity.HasData(new Location
            {
                Id = SystemDefaults.LocationId,
                HouseholdId = SystemDefaults.HouseholdId,
                Name = "未設定",
                SortOrder = 0
            });
        });

        modelBuilder.Entity<StockLot>(entity =>
        {
            entity.ToTable("stock_lots", schema);
            entity.Property(x => x.CurrentQuantity).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.HouseholdId, x.ProductId, x.LocationId, x.ExpiresOn });
            entity.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockOperation>(entity =>
        {
            entity.ToTable("stock_operations", schema);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.RequestedQuantity).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.HouseholdId, x.IdempotencyKey }).IsUnique();
            entity.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements", schema);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.QuantityDelta).HasPrecision(18, 4);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasIndex(x => new { x.ProductId, x.OccurredAt });
            entity.HasOne<StockOperation>().WithMany(x => x.Movements).HasForeignKey(x => x.StockOperationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StockLot>().WithMany().HasForeignKey(x => x.StockLotId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
