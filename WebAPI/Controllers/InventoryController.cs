using InventoryAPI.Application.Inventory;
using InventoryAPI.Contracts.Inventory;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

/// <summary>
/// 在庫管理API
/// </summary>
/// <param name="db"></param>
/// <param name="inventoryService"></param>
[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController(ApplicationDbContext db, InventoryService inventoryService) : ControllerBase
{
    public const int DefaultLimit = 20;
    public const int MaximumLimit = 100;

    /// <summary>
    /// 在庫一覧取得
    /// </summary>
    /// <param name="productId"></param>
    /// <param name="locationId"></param>
    /// <param name="expiringBefore"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventoryLotResponse>>> GetInventory(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? locationId,
        [FromQuery] DateOnly? expiringBefore,
        [FromQuery] string? query,
        [FromQuery] string sortBy = "name",
        [FromQuery] string sortOrder = "asc",
        [FromQuery] int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var lots = db.StockLots.AsNoTracking()
            .Where(x => x.HouseholdId == SystemDefaults.HouseholdId && x.CurrentQuantity > 0);
        if (productId.HasValue)
            lots = lots.Where(x => x.ProductId == productId.Value);
        if (locationId.HasValue)
            lots = lots.Where(x => x.LocationId == locationId.Value);
        if (expiringBefore.HasValue)
            lots = lots.Where(x => x.ExpiresOn != null && x.ExpiresOn <= expiringBefore.Value);

        var rows =
            from lot in lots
            join product in db.Products.AsNoTracking() on lot.ProductId equals product.Id
            join location in db.Locations.AsNoTracking() on lot.LocationId equals location.Id
            select new
            {
                LotId = lot.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                product.Barcode,
                product.Unit,
                LocationId = location.Id,
                LocationName = location.Name,
                lot.ExpiresOn,
                Quantity = lot.CurrentQuantity
            };

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim();
            rows = rows.Where(row =>
                EF.Functions.ILike(row.ProductName, $"%{search}%") ||
                (row.Barcode != null && row.Barcode.Contains(search)) ||
                EF.Functions.ILike(row.LocationName, $"%{search}%"));
        }

        var isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        var orderedRows = sortBy.ToLowerInvariant() switch
        {
            "expiration" => isDescending
                ? rows.OrderByDescending(row => row.ExpiresOn.HasValue).ThenByDescending(row => row.ExpiresOn).ThenBy(row => row.ProductName)
                : rows.OrderBy(row => !row.ExpiresOn.HasValue).ThenBy(row => row.ExpiresOn).ThenBy(row => row.ProductName),
            "quantity" => isDescending
                ? rows.OrderByDescending(row => (double)row.Quantity).ThenBy(row => row.ProductName)
                : rows.OrderBy(row => (double)row.Quantity).ThenBy(row => row.ProductName),
            _ => isDescending
                ? rows.OrderByDescending(row => row.ProductName).ThenBy(row => row.ExpiresOn)
                : rows.OrderBy(row => row.ProductName).ThenBy(row => row.ExpiresOn)
        };

        return await orderedRows
            .Take(Math.Clamp(limit, 1, MaximumLimit))
            .Select(row => new InventoryLotResponse(
                row.LotId,
                row.ProductId,
                row.ProductName,
                row.Barcode,
                row.Unit,
                row.LocationId,
                row.LocationName,
                row.ExpiresOn,
                row.Quantity))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 在庫受入
    /// </summary>
    /// <param name="request"></param>
    /// <param name="idempotencyKey"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("receive")]
    public async Task<ActionResult<StockOperationResponse>> Receive(
        ReceiveStockRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryValidateIdempotencyKey(idempotencyKey, out var key, out var error))
            return error!;

        var result = await inventoryService.ReceiveAsync(request, key!, cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// 在庫消費
    /// </summary>
    /// <param name="request"></param>
    /// <param name="idempotencyKey"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("consume")]
    public async Task<ActionResult<StockOperationResponse>> Consume(
        ConsumeStockRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryValidateIdempotencyKey(idempotencyKey, out var key, out var error))
            return error!;

        var result = await inventoryService.ConsumeAsync(request, key!, cancellationToken);
        return MapResult(result);
    }

    [HttpPost("discard")]
    public async Task<ActionResult<StockOperationResponse>> Discard(
        DiscardStockRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryValidateIdempotencyKey(idempotencyKey, out var key, out var error))
            return error!;

        return MapResult(await inventoryService.DiscardAsync(request, key!, cancellationToken));
    }

    [HttpPost("adjust")]
    public async Task<ActionResult<StockOperationResponse>> Adjust(
        AdjustStockRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryValidateIdempotencyKey(idempotencyKey, out var key, out var error))
            return error!;

        return MapResult(await inventoryService.AdjustAsync(request, key!, cancellationToken));
    }

    /// <summary>
    /// 指定した在庫操作を逆仕訳で取り消します。
    /// </summary>
    [HttpPost("operations/{operationId:guid}/reverse")]
    public async Task<ActionResult<StockOperationResponse>> Reverse(
        Guid operationId,
        ReverseStockOperationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryValidateIdempotencyKey(idempotencyKey, out var key, out var error))
            return error!;

        return MapResult(await inventoryService.ReverseAsync(
            operationId, request, key!, cancellationToken));
    }

    /// <summary>
    /// 在庫操作結果をHTTPレスポンスにマッピングする
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    private ActionResult<StockOperationResponse> MapResult(InventoryResult result) => result.Status switch
    {
        InventoryResultStatus.Success => Ok(ToResponse(result.Operation!)),
        InventoryResultStatus.NotFound => NotFound(new ProblemDetails
        {
            Title = "商品または保管場所が見つかりません。",
            Status = StatusCodes.Status404NotFound
        }),
        InventoryResultStatus.InsufficientStock => Conflict(new ProblemDetails
        {
            Title = "在庫が不足しています。",
            Status = StatusCodes.Status409Conflict
        }),
        InventoryResultStatus.AlreadyReversed => Conflict(new ProblemDetails
        {
            Title = "指定した在庫操作は既に取り消されています。",
            Status = StatusCodes.Status409Conflict
        }),
        InventoryResultStatus.CannotReverse => Conflict(new ProblemDetails
        {
            Title = "現在庫との整合性を保てないため、指定した在庫操作を取り消せません。",
            Status = StatusCodes.Status409Conflict
        }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    /// <summary>
    /// Idempotency-Keyヘッダーの値を検証する
    /// 1〜100文字であることを確認する
    /// </summary>
    /// <param name="value"></param>
    /// <param name="key"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    private bool TryValidateIdempotencyKey(string? value, out string? key, out ActionResult<StockOperationResponse>? error)
    {
        key = value?.Trim();
        error = null;
        if (!string.IsNullOrEmpty(key) && key.Length <= 100)
            return true;

        error = BadRequest(new ProblemDetails
        {
            Title = "Idempotency-Keyヘッダーは1〜100文字で指定してください。",
            Status = StatusCodes.Status400BadRequest
        });
        return false;
    }

    internal static StockOperationResponse ToResponse(Domain.Inventory.StockOperation operation) => new(
        operation.Id,
        operation.Type,
        operation.RequestedQuantity,
        operation.OccurredAt,
        operation.Movements.Select(x => new StockMovementResponse(
            x.Id, x.StockLotId, x.ReversesMovementId, x.ProductId, x.LocationId, x.Type,
            x.QuantityDelta, x.OccurredAt, x.Note)).ToList());
}
