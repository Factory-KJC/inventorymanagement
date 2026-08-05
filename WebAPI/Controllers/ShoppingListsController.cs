using InventoryAPI.Contracts.Shopping;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Shopping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/shopping-lists/current")]
public sealed class ShoppingListsController(ApplicationDbContext db, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ShoppingListResponse>> GetCurrent(CancellationToken cancellationToken)
    {
        var list = await FindCurrentAsync(cancellationToken);
        return list is null
            ? Ok(new ShoppingListResponse(null, ShoppingListStatus.Active, null, []))
            : Ok(ToResponse(list));
    }

    [HttpPost("items")]
    public async Task<ActionResult<ShoppingListResponse>> AddItem(
        AddShoppingItemRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return ValidationProblem("品名は必須です。");
        if (request.ProductId.HasValue && !await db.Products.AnyAsync(
                x => x.Id == request.ProductId && x.HouseholdId == SystemDefaults.HouseholdId,
                cancellationToken))
            return NotFound(new ProblemDetails { Title = "商品が見つかりません。", Status = 404 });

        var list = await GetOrCreateCurrentAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        list.Items.Add(new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = list.Id,
            ProductId = request.ProductId,
            Name = name,
            Quantity = request.Quantity,
            Source = ShoppingItemSource.Manual,
            Status = ShoppingItemStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(list));
    }

    [HttpPost("generate")]
    public async Task<ActionResult<ShoppingListResponse>> Generate(CancellationToken cancellationToken)
    {
        var list = await GetOrCreateCurrentAsync(cancellationToken);
        var stockRows = await db.StockLots.Where(x => x.HouseholdId == SystemDefaults.HouseholdId)
            .Select(x => new { x.ProductId, x.CurrentQuantity })
            .ToListAsync(cancellationToken);
        var stock = stockRows.GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.CurrentQuantity));
        var products = await db.Products.Where(x =>
                x.HouseholdId == SystemDefaults.HouseholdId && x.ReorderPoint != null)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var product in products)
        {
            var current = stock.GetValueOrDefault(product.Id);
            if (current >= product.ReorderPoint!.Value)
                continue;

            var existing = list.Items.FirstOrDefault(x =>
                x.ProductId == product.Id && x.Source == ShoppingItemSource.ReorderSuggestion);
            if (existing is { Status: ShoppingItemStatus.Dismissed or ShoppingItemStatus.Purchased })
                continue;

            var target = product.TargetQuantity ?? product.ReorderPoint.Value;
            var suggested = Math.Max(target - current, 1);
            if (existing is null)
            {
                list.Items.Add(new ShoppingListItem
                {
                    Id = Guid.NewGuid(), ShoppingListId = list.Id, ProductId = product.Id,
                    Name = product.Name, Quantity = suggested, Source = ShoppingItemSource.ReorderSuggestion,
                    Status = ShoppingItemStatus.Pending, CreatedAt = now, UpdatedAt = now
                });
            }
            else
            {
                existing.Name = product.Name;
                existing.Quantity = suggested;
                existing.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(list));
    }

    [HttpPatch("items/{id:guid}")]
    public async Task<ActionResult<ShoppingListResponse>> UpdateItem(
        Guid id,
        UpdateShoppingItemRequest request,
        CancellationToken cancellationToken)
    {
        var list = await FindCurrentAsync(cancellationToken);
        var item = list?.Items.SingleOrDefault(x => x.Id == id);
        if (list is null || item is null)
            return NotFound();
        if (!request.Quantity.HasValue && !request.Status.HasValue)
            return ValidationProblem("数量または状態を指定してください。");

        if (request.Quantity.HasValue)
            item.Quantity = request.Quantity.Value;
        if (request.Status.HasValue)
            item.Status = request.Status.Value;
        item.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(list));
    }

    [HttpPost("complete")]
    public async Task<ActionResult<ShoppingListResponse>> Complete(CancellationToken cancellationToken)
    {
        var list = await FindCurrentAsync(cancellationToken);
        if (list is null)
            return NotFound();
        list.Status = ShoppingListStatus.Completed;
        list.CompletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(list));
    }

    private async Task<ShoppingList?> FindCurrentAsync(CancellationToken cancellationToken) =>
        await db.ShoppingLists.Include(x => x.Items).SingleOrDefaultAsync(
            x => x.HouseholdId == SystemDefaults.HouseholdId && x.Status == ShoppingListStatus.Active,
            cancellationToken);

    private async Task<ShoppingList> GetOrCreateCurrentAsync(CancellationToken cancellationToken)
    {
        var list = await FindCurrentAsync(cancellationToken);
        if (list is not null)
            return list;
        list = new ShoppingList
        {
            Id = Guid.NewGuid(), HouseholdId = SystemDefaults.HouseholdId,
            Status = ShoppingListStatus.Active, CreatedAt = timeProvider.GetUtcNow()
        };
        db.ShoppingLists.Add(list);
        return list;
    }

    private static ShoppingListResponse ToResponse(ShoppingList list) => new(
        list.Id, list.Status, list.CreatedAt,
        list.Items.OrderBy(x => x.Status).ThenBy(x => x.Name).Select(x => new ShoppingItemResponse(
            x.Id, x.ProductId, x.Name, x.Quantity, x.Source, x.Status)).ToList());
}
