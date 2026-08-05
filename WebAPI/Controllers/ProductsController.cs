using InventoryAPI.Contracts.Catalog;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

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
        var products = db.Products.AsNoTracking().Where(x => x.HouseholdId == SystemDefaults.HouseholdId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim();
            products = products.Where(x => EF.Functions.ILike(x.Name, $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var normalizedBarcode = barcode.Trim();
            products = products.Where(x => x.Barcode == normalizedBarcode);
        }

        return await products.OrderBy(x => x.Name)
            .Select(x => new ProductResponse(x.Id, x.Name, x.Barcode, x.Unit, x.ReorderPoint, x.TargetQuantity))
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
        var product = await db.Products.AsNoTracking()
            .Where(x => x.Id == id && x.HouseholdId == SystemDefaults.HouseholdId)
            .Select(x => new ProductResponse(x.Id, x.Name, x.Barcode, x.Unit, x.ReorderPoint, x.TargetQuantity))
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
        if (request.ReorderPoint.HasValue && request.TargetQuantity.HasValue && request.TargetQuantity < request.ReorderPoint)
            return ValidationProblem("目標在庫は補充点以上にしてください。");
        if (barcode is not null && await db.Products.AnyAsync(x => x.HouseholdId == SystemDefaults.HouseholdId && x.Barcode == barcode, cancellationToken))
            return Conflict(new ProblemDetails { Title = "このJANコードは登録済みです。", Status = StatusCodes.Status409Conflict });

        var now = timeProvider.GetUtcNow();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            HouseholdId = SystemDefaults.HouseholdId,
            Name = name,
            Barcode = barcode,
            Unit = unit,
            ReorderPoint = request.ReorderPoint,
            TargetQuantity = request.TargetQuantity,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        var response = new ProductResponse(product.Id, product.Name, product.Barcode, product.Unit, product.ReorderPoint, product.TargetQuantity);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, response);
    }
}
