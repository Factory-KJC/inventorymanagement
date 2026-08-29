using System.ComponentModel.DataAnnotations;
using InventoryAPI.Contracts.Inventory;
using InventoryAPI.Domain.Shopping;

namespace InventoryAPI.Contracts.Shopping;

public sealed record AddShoppingItemRequest(
    Guid? ProductId,
    [Required, StringLength(200)] string Name,
    [Range(typeof(decimal), "0.0001", "999999999")] decimal Quantity);

public sealed record UpdateShoppingItemRequest(
    [Range(typeof(decimal), "0.0001", "999999999")] decimal? Quantity,
    ShoppingItemStatus? Status);

public sealed record ReceiveShoppingListRequest(
    [Required, MinLength(1)] IReadOnlyList<ReceiveShoppingItemRequest> Items);

public sealed record ReceiveShoppingItemRequest(
    Guid ItemId,
    Guid LocationId,
    DateOnly? ExpiresOn);

public sealed record ReceiveShoppingListResponse(
    Guid ShoppingListId,
    ShoppingListStatus Status,
    StockOperationResponse Operation);

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

/// <summary>
/// 現在の買い物リストを印刷レイアウトへ渡すための、表示に依存しないデータを表します。
/// </summary>
public sealed record ShoppingListPrintResponse(
    Guid ShoppingListId,
    DateTimeOffset CreatedAt,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ShoppingListPrintItemResponse> Items);

/// <summary>
/// 印刷対象となる未購入の買い物項目を表します。
/// </summary>
public sealed record ShoppingListPrintItemResponse(
    Guid ItemId,
    string Name,
    decimal Quantity,
    string? Unit,
    ShoppingItemSource Source);
