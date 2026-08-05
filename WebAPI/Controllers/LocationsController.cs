using InventoryAPI.Contracts.Inventory;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/locations")]
public sealed class LocationsController(ApplicationDbContext db) : ControllerBase
{
    /// <summary>
    /// 保管場所一覧取得
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationResponse>>> GetLocations(CancellationToken cancellationToken) =>
        await db.Locations.AsNoTracking()
            .Where(x => x.HouseholdId == SystemDefaults.HouseholdId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LocationResponse(x.Id, x.Name, x.SortOrder))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// 保管場所作成
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<LocationResponse>> CreateLocation(
        CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return ValidationProblem("保管場所名は必須です。");
        if (await db.Locations.AnyAsync(x => x.HouseholdId == SystemDefaults.HouseholdId && x.Name == name, cancellationToken))
            return Conflict(new ProblemDetails { Title = "同名の保管場所が存在します。", Status = StatusCodes.Status409Conflict });

        var location = new Location
        {
            Id = Guid.NewGuid(),
            HouseholdId = SystemDefaults.HouseholdId,
            Name = name,
            SortOrder = request.SortOrder
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/locations/{location.Id}", new LocationResponse(location.Id, location.Name, location.SortOrder));
    }
}
