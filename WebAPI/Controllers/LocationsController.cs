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
public sealed class LocationsController(ApplicationDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// 保管場所一覧取得
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationResponse>>> GetLocations(CancellationToken cancellationToken) =>
        await db.Locations.AsNoTracking()
            .Where(x => x.HouseholdId == SystemDefaults.HouseholdId && !x.IsDeleted)
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

    /// <summary>
    /// 保管場所の名称と表示順を更新します。
    /// </summary>
    /// <param name="id">更新対象の保管場所ID。</param>
    /// <param name="request">更新後の名称と表示順。</param>
    /// <param name="cancellationToken">処理のキャンセル通知。</param>
    /// <returns>更新後の保管場所。対象が存在しない場合は404を返します。</returns>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<LocationResponse>> UpdateLocation(
        Guid id,
        UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var location = await db.Locations.SingleOrDefaultAsync(
            location => location.Id == id && location.HouseholdId == SystemDefaults.HouseholdId && !location.IsDeleted,
            cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return ValidationProblem("保管場所名は必須です。");
        }

        if (await db.Locations.AnyAsync(
                other => other.HouseholdId == SystemDefaults.HouseholdId &&
                         other.Id != id &&
                         other.Name == name,
                cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "同名の保管場所が存在します。",
                Status = StatusCodes.Status409Conflict,
            });
        }

        location.Name = name;
        location.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new LocationResponse(location.Id, location.Name, location.SortOrder));
    }

    /// <summary>保管場所を履歴から参照可能な状態で論理削除します。</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLocation(Guid id, CancellationToken cancellationToken)
    {
        var location = await db.Locations.SingleOrDefaultAsync(
            location => location.Id == id && location.HouseholdId == SystemDefaults.HouseholdId && !location.IsDeleted,
            cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        location.IsDeleted = true;
        location.DeletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>入力名と2文字以上の部分一致をする削除済み保管場所を返します。</summary>
    [HttpGet("deleted-suggestions")]
    public async Task<ActionResult<IReadOnlyList<DeletedLocationSuggestionResponse>>> GetDeletedSuggestions(
        [FromQuery] string name,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length < 2)
        {
            return Ok(Array.Empty<DeletedLocationSuggestionResponse>());
        }

        var deletedLocations = await db.Locations.AsNoTracking()
            .Where(location => location.HouseholdId == SystemDefaults.HouseholdId && location.IsDeleted)
            .ToListAsync(cancellationToken);
        return deletedLocations
            .Where(location => MasterNameMatcher.IsPartialMatch(location.Name, normalizedName))
            .OrderByDescending(location => string.Equals(location.Name, normalizedName, StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(location => location.Name)
            .Select(location => new DeletedLocationSuggestionResponse(location.Id, location.Name, location.SortOrder))
            .Take(10)
            .ToList();
    }

    /// <summary>削除済み保管場所を入力された最新情報で復元します。</summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<LocationResponse>> RestoreLocation(
        Guid id,
        UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var location = await db.Locations.SingleOrDefaultAsync(
            location => location.Id == id && location.HouseholdId == SystemDefaults.HouseholdId && location.IsDeleted,
            cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return ValidationProblem("保管場所名は必須です。");
        }

        if (await db.Locations.AnyAsync(other => other.HouseholdId == SystemDefaults.HouseholdId &&
                !other.IsDeleted && other.Id != id && other.Name == name, cancellationToken))
        {
            return Conflict(new ProblemDetails { Title = "同名の保管場所が存在します。", Status = StatusCodes.Status409Conflict });
        }

        location.Name = name;
        location.SortOrder = request.SortOrder;
        location.IsDeleted = false;
        location.DeletedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new LocationResponse(location.Id, location.Name, location.SortOrder));
    }
}
