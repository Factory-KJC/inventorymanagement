using InventoryAPI.Domain.Catalog;
using InventoryAPI.Domain.Households;
using InventoryAPI.Domain.Inventory;
using InventoryAPI.Domain.Shopping;
using InventoryAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Data;

/// <summary>
/// Home Stockが永続化するエンティティへの入口です。
/// 個別のテーブル設定はData.Configurations名前空間へ分離しています。
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<StockOperation> StockOperations => Set<StockOperation>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
