using InventoryAPI.Contracts.Inventory;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/movements")]
public sealed class MovementsController(ApplicationDbContext db) : ControllerBase
{
    /// <summary>
    /// 在庫操作履歴一覧取得
    /// </summary>
    /// <param name="productId"></param>
    /// <param name="limit"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockMovementResponse>>> GetMovements(
        [FromQuery] Guid? productId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var movements =
            from movement in db.StockMovements.AsNoTracking()
            join operation in db.StockOperations.AsNoTracking() on movement.StockOperationId equals operation.Id
            where operation.HouseholdId == SystemDefaults.HouseholdId
            select movement;
        if (productId.HasValue)
            movements = movements.Where(x => x.ProductId == productId.Value);

        return await movements.OrderByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new StockMovementResponse(
                x.Id, x.StockLotId, x.ProductId, x.LocationId, x.Type,
                x.QuantityDelta, x.OccurredAt, x.Note))
            .ToListAsync(cancellationToken);
    }
}
