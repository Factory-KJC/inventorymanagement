using InventoryAPI.Contracts.Catalog;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

/// <summary>
/// 商品マスターの検索と登録を提供します。
/// </summary>
[ApiController]
[Authorize]
[Route("api/products")]
public sealed class ProductsController(ApplicationDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// 商品一覧取得
    /// </summary>
    /// <param name="query"></param>
    /// <param name="barcode"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetProducts(
        [FromQuery] string? query,
        [FromQuery] string? barcode,
        CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking().Where(x => x.HouseholdId == SystemDefaults.HouseholdId && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim();
            products = products.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%") ||
                (x.ProductGroupId != null && db.ProductGroups.Any(group =>
                    group.Id == x.ProductGroupId && EF.Functions.ILike(group.Name, $"%{search}%"))));
        }

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var normalizedBarcode = barcode.Trim();
            products = products.Where(x => x.Barcode == normalizedBarcode);
        }

        return await (from product in products
            join productGroup in db.ProductGroups.AsNoTracking() on product.ProductGroupId equals productGroup.Id into groups
            from productGroup in groups.DefaultIfEmpty()
            orderby product.Name
            select new ProductResponse(
                product.Id,
                product.Name,
                product.Barcode,
                product.Unit,
                productGroup != null ? productGroup.ReorderPoint : product.ReorderPoint,
                productGroup != null ? productGroup.TargetQuantity : product.TargetQuantity,
                product.ProductGroupId,
                productGroup != null ? productGroup.Name : null))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 商品取得
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(Guid id, CancellationToken cancellationToken)
    {
        var product = await (from candidate in db.Products.AsNoTracking()
            join productGroup in db.ProductGroups.AsNoTracking() on candidate.ProductGroupId equals productGroup.Id into groups
            from productGroup in groups.DefaultIfEmpty()
            where candidate.Id == id && candidate.HouseholdId == SystemDefaults.HouseholdId && !candidate.IsDeleted
            select new ProductResponse(
                candidate.Id,
                candidate.Name,
                candidate.Barcode,
                candidate.Unit,
                productGroup != null ? productGroup.ReorderPoint : candidate.ReorderPoint,
                productGroup != null ? productGroup.TargetQuantity : candidate.TargetQuantity,
                candidate.ProductGroupId,
                productGroup != null ? productGroup.Name : null))
            .SingleOrDefaultAsync(cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>
    /// 商品作成
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var unit = request.Unit.Trim();
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

        if (name.Length == 0 || unit.Length == 0)
            return ValidationProblem("商品名と単位は必須です。");
        if (!BarcodeValidator.IsValid(barcode))
            return ValidationProblem("JANコードの形式またはチェックディジットが正しくありません。");
        if (request.ReorderPoint.HasValue &&
            request.TargetQuantity.HasValue &&
            request.TargetQuantity < request.ReorderPoint)
            return ValidationProblem("目標在庫は補充点以上にしてください。");
        if (barcode is not null && await db.Products.AnyAsync(
                product => product.HouseholdId == SystemDefaults.HouseholdId && product.Barcode == barcode,
                cancellationToken))
            return Conflict(new ProblemDetails { Title = "このJANコードは登録済みです。", Status = StatusCodes.Status409Conflict });

        var now = timeProvider.GetUtcNow();
        var productGroup = await GetOrCreateProductGroupAsync(request.ProductGroupName, unit, request.ReorderPoint, request.TargetQuantity, now, cancellationToken);
        var product = new Product
        {
            Id = Guid.NewGuid(),
            HouseholdId = SystemDefaults.HouseholdId,
            ProductGroupId = productGroup?.Id,
            Name = name,
            Barcode = barcode,
            Unit = unit,
            ReorderPoint = productGroup is null ? request.ReorderPoint : null,
            TargetQuantity = productGroup is null ? request.TargetQuantity : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, ToResponse(product, productGroup));
    }

    /// <summary>
    /// 商品名、JANコード、単位、補充基準を一括で更新します。
    /// </summary>
    /// <param name="id">更新対象の商品ID。</param>
    /// <param name="request">更新後の商品情報。JANコードを解除する場合はnullを指定します。</param>
    /// <param name="cancellationToken">処理のキャンセル通知。</param>
    /// <returns>更新後の商品。対象が存在しない場合は404を返します。</returns>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> UpdateProduct(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(
                product => product.Id == id && product.HouseholdId == SystemDefaults.HouseholdId && !product.IsDeleted,
            cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        var unit = request.Unit.Trim();
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

        if (name.Length == 0 || unit.Length == 0)
        {
            return ValidationProblem("商品名と単位は必須です。");
        }

        if (!BarcodeValidator.IsValid(barcode))
        {
            return ValidationProblem("JANコードの形式またはチェックディジットが正しくありません。");
        }

        if (request.ReorderPoint.HasValue &&
            request.TargetQuantity.HasValue &&
            request.TargetQuantity < request.ReorderPoint)
        {
            return ValidationProblem("目標在庫は補充点以上にしてください。");
        }

        if (barcode is not null && await db.Products.AnyAsync(
                other => other.HouseholdId == SystemDefaults.HouseholdId &&
                         other.Id != id &&
                         other.Barcode == barcode,
                cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "このJANコードは登録済みです。",
                Status = StatusCodes.Status409Conflict
            });
        }

        var now = timeProvider.GetUtcNow();
        var productGroup = await GetOrCreateProductGroupAsync(request.ProductGroupName, unit, request.ReorderPoint, request.TargetQuantity, now, cancellationToken);
        product.Name = name;
        product.Barcode = barcode;
        product.Unit = unit;
        product.ProductGroupId = productGroup?.Id;
        product.ReorderPoint = productGroup is null ? request.ReorderPoint : null;
        product.TargetQuantity = productGroup is null ? request.TargetQuantity : null;
        product.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(product, productGroup));
    }

    /// <summary>商品を履歴から参照可能な状態で論理削除します。</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(
            product => product.Id == id && product.HouseholdId == SystemDefaults.HouseholdId && !product.IsDeleted,
            cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        product.IsDeleted = true;
        product.DeletedAt = timeProvider.GetUtcNow();
        product.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>入力名と2文字以上の部分一致をする削除済み商品を返します。</summary>
    [HttpGet("deleted-suggestions")]
    public async Task<ActionResult<IReadOnlyList<DeletedProductSuggestionResponse>>> GetDeletedSuggestions(
        [FromQuery] string name,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length < 2)
        {
            return Ok(Array.Empty<DeletedProductSuggestionResponse>());
        }

        var deletedProducts = await db.Products.AsNoTracking()
            .Where(product => product.HouseholdId == SystemDefaults.HouseholdId && product.IsDeleted)
            .ToListAsync(cancellationToken);
        return deletedProducts
            .Where(product => MasterNameMatcher.IsPartialMatch(product.Name, normalizedName))
            .OrderByDescending(product => string.Equals(product.Name, normalizedName, StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(product => product.Name)
            .Select(product => new DeletedProductSuggestionResponse(product.Id, product.Name, product.Barcode, product.Unit))
            .Take(10)
            .ToList();
    }

    /// <summary>削除済み商品を入力された最新情報で復元します。</summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<ProductResponse>> RestoreProduct(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(
            product => product.Id == id && product.HouseholdId == SystemDefaults.HouseholdId && product.IsDeleted,
            cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        var unit = request.Unit.Trim();
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        if (name.Length == 0 || unit.Length == 0 || !BarcodeValidator.IsValid(barcode))
        {
            return ValidationProblem("商品名、単位、JANコードを確認してください。");
        }

        if (await db.Products.AnyAsync(other => other.HouseholdId == SystemDefaults.HouseholdId &&
                !other.IsDeleted && other.Id != id &&
                ((barcode != null && other.Barcode == barcode) || other.Name == name), cancellationToken))
        {
            return Conflict(new ProblemDetails { Title = "同じ商品名またはJANコードの商品が存在します。", Status = StatusCodes.Status409Conflict });
        }

        var now = timeProvider.GetUtcNow();
        var productGroup = await GetOrCreateProductGroupAsync(request.ProductGroupName, unit, request.ReorderPoint, request.TargetQuantity, now, cancellationToken);
        product.Name = name;
        product.Barcode = barcode;
        product.Unit = unit;
        product.ProductGroupId = productGroup?.Id;
        product.ReorderPoint = productGroup is null ? request.ReorderPoint : null;
        product.TargetQuantity = productGroup is null ? request.TargetQuantity : null;
        product.IsDeleted = false;
        product.DeletedAt = null;
        product.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(product, productGroup));
    }

    private async Task<ProductGroup?> GetOrCreateProductGroupAsync(
        string? requestedName,
        string unit,
        decimal? reorderPoint,
        decimal? targetQuantity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var groupName = string.IsNullOrWhiteSpace(requestedName) ? null : requestedName.Trim();
        if (groupName is null)
        {
            return null;
        }

        var group = await db.ProductGroups.SingleOrDefaultAsync(
            candidate => candidate.HouseholdId == SystemDefaults.HouseholdId && candidate.Name == groupName,
            cancellationToken);
        if (group is null)
        {
            group = new ProductGroup
            {
                Id = Guid.NewGuid(),
                HouseholdId = SystemDefaults.HouseholdId,
                Name = groupName,
                Unit = unit,
                CreatedAt = now
            };
            db.ProductGroups.Add(group);
        }

        group.Unit = unit;
        group.ReorderPoint = reorderPoint;
        group.TargetQuantity = targetQuantity;
        group.UpdatedAt = now;
        return group;
    }

    private static ProductResponse ToResponse(Product product, ProductGroup? group) => new(
        product.Id,
        product.Name,
        product.Barcode,
        product.Unit,
        group?.ReorderPoint ?? product.ReorderPoint,
        group?.TargetQuantity ?? product.TargetQuantity,
        product.ProductGroupId,
        group?.Name);
}
