using InventoryAPI.Contracts.Dashboard;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Shopping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(ApplicationDbContext db, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken cancellationToken)
    {
        var productCount = await db.Products.CountAsync(x => x.HouseholdId == SystemDefaults.HouseholdId, cancellationToken);
        var quantityRows = await db.StockLots.Where(x => x.HouseholdId == SystemDefaults.HouseholdId)
            .Select(x => new { x.ProductId, x.CurrentQuantity })
            .ToListAsync(cancellationToken);
        var quantityMap = quantityRows.GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.CurrentQuantity));
        var reorderProducts = await db.Products.Where(x =>
                x.HouseholdId == SystemDefaults.HouseholdId && x.ReorderPoint != null)
            .Select(x => new { x.Id, ReorderPoint = x.ReorderPoint!.Value })
            .ToListAsync(cancellationToken);
        var lowStockCount = reorderProducts.Count(x => quantityMap.GetValueOrDefault(x.Id) < x.ReorderPoint);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var deadline = today.AddDays(7);
        var expiringSoonCount = await db.StockLots.CountAsync(x =>
            x.HouseholdId == SystemDefaults.HouseholdId && x.CurrentQuantity > 0 &&
            x.ExpiresOn != null && x.ExpiresOn >= today && x.ExpiresOn <= deadline, cancellationToken);
        var shoppingItemCount = await (
            from item in db.ShoppingListItems
            join list in db.ShoppingLists on item.ShoppingListId equals list.Id
            where list.HouseholdId == SystemDefaults.HouseholdId && list.Status == ShoppingListStatus.Active &&
                  item.Status == ShoppingItemStatus.Pending
            select item).CountAsync(cancellationToken);
        return new DashboardResponse(productCount, lowStockCount, expiringSoonCount, shoppingItemCount);
    }

    [HttpGet("{category}")]
    public async Task<ActionResult<DashboardListResponse>> GetList(
        string category, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 20);
        var items = category.ToLowerInvariant() switch
        {
            "products" => await GetProductsAsync(cancellationToken),
            "low-stock" => await GetLowStockAsync(cancellationToken),
            "expiring" => await GetExpiringAsync(cancellationToken),
            "shopping" => await GetShoppingAsync(cancellationToken),
            _ => null
        };
        if (items is null)
            return NotFound(new ProblemDetails { Title = "ダッシュボード項目が見つかりません。", Status = 404 });

        return new DashboardListResponse(category, page, pageSize, items.Count,
            items.Skip((page - 1) * pageSize).Take(pageSize).ToList());
    }

    private async Task<List<DashboardItemResponse>> GetProductsAsync(CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking().Where(x => x.HouseholdId == SystemDefaults.HouseholdId)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var quantities = await GetQuantityMapAsync(cancellationToken);
        return products.Select(x => new DashboardItemResponse(
            x.Id, x.Name, x.Barcode is null ? x.Unit : $"JAN {x.Barcode} · {x.Unit}", quantities.GetValueOrDefault(x.Id))).ToList();
    }

    private async Task<List<DashboardItemResponse>> GetLowStockAsync(CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking().Where(x =>
                x.HouseholdId == SystemDefaults.HouseholdId && x.ReorderPoint != null)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var quantities = await GetQuantityMapAsync(cancellationToken);
        return products.Where(x => quantities.GetValueOrDefault(x.Id) < x.ReorderPoint!.Value)
            .Select(x => new DashboardItemResponse(x.Id, x.Name,
                $"補充点 {x.ReorderPoint} {x.Unit} / 目標 {x.TargetQuantity ?? x.ReorderPoint} {x.Unit}",
                quantities.GetValueOrDefault(x.Id))).ToList();
    }

    private async Task<List<DashboardItemResponse>> GetExpiringAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var deadline = today.AddDays(7);
        return await (from lot in db.StockLots.AsNoTracking()
            join product in db.Products.AsNoTracking() on lot.ProductId equals product.Id
            join location in db.Locations.AsNoTracking() on lot.LocationId equals location.Id
            where lot.HouseholdId == SystemDefaults.HouseholdId && lot.CurrentQuantity > 0 &&
                  lot.ExpiresOn != null && lot.ExpiresOn >= today && lot.ExpiresOn <= deadline
            orderby lot.ExpiresOn, product.Name
            select new DashboardItemResponse(lot.Id, product.Name,
                $"期限 {lot.ExpiresOn} · {location.Name} · {product.Unit}", lot.CurrentQuantity))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<DashboardItemResponse>> GetShoppingAsync(CancellationToken cancellationToken) =>
        await (from item in db.ShoppingListItems.AsNoTracking()
            join list in db.ShoppingLists.AsNoTracking() on item.ShoppingListId equals list.Id
            where list.HouseholdId == SystemDefaults.HouseholdId && list.Status == ShoppingListStatus.Active &&
                  item.Status == ShoppingItemStatus.Pending
            orderby item.Name
            select new DashboardItemResponse(item.Id, item.Name,
                item.Source == ShoppingItemSource.ReorderSuggestion ? "在庫から提案" : "手動追加", item.Quantity))
            .ToListAsync(cancellationToken);

    private async Task<Dictionary<Guid, decimal>> GetQuantityMapAsync(CancellationToken cancellationToken)
    {
        var rows = await db.StockLots.AsNoTracking().Where(x => x.HouseholdId == SystemDefaults.HouseholdId)
            .Select(x => new { x.ProductId, x.CurrentQuantity }).ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(y => y.CurrentQuantity));
    }
}
