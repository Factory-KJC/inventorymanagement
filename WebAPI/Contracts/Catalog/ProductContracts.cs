using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Contracts.Catalog;

public sealed record CreateProductRequest(
    [Required, StringLength(200)] string Name,
    [StringLength(32)] string? Barcode,
    [Required, StringLength(20)] string Unit,
    [Range(0, 999999999)] decimal? ReorderPoint,
    [Range(0, 999999999)] decimal? TargetQuantity);

public sealed record UpdateProductRequest(
    [Required, StringLength(200)] string Name,
    [StringLength(32)] string? Barcode,
    [Required, StringLength(20)] string Unit,
    [Range(0, 999999999)] decimal? ReorderPoint,
    [Range(0, 999999999)] decimal? TargetQuantity);

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Barcode,
    string Unit,
    decimal? ReorderPoint,
    decimal? TargetQuantity);

public sealed record DeletedProductSuggestionResponse(Guid Id, string Name, string? Barcode, string Unit);
