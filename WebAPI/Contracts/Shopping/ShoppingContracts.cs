using System.ComponentModel.DataAnnotations;
using InventoryAPI.Domain.Shopping;

namespace InventoryAPI.Contracts.Shopping;

public sealed record AddShoppingItemRequest(
    Guid? ProductId,
    [Required, StringLength(200)] string Name,
    [Range(typeof(decimal), "0.0001", "999999999")] decimal Quantity);

public sealed record UpdateShoppingItemRequest(
    [Range(typeof(decimal), "0.0001", "999999999")] decimal? Quantity,
    ShoppingItemStatus? Status);

public sealed record ShoppingListResponse(
    Guid? Id,
    ShoppingListStatus Status,
    DateTimeOffset? CreatedAt,
    IReadOnlyList<ShoppingItemResponse> Items);

public sealed record ShoppingItemResponse(
    Guid Id,
    Guid? ProductId,
    string Name,
    decimal Quantity,
    ShoppingItemSource Source,
    ShoppingItemStatus Status);
