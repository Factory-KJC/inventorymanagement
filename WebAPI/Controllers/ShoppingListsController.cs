using InventoryAPI.Application.Shopping;
using InventoryAPI.Contracts.Shopping;
using InventoryAPI.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers;

/// <summary>
/// 現在有効な買い物リストを操作します。
/// </summary>
[ApiController]
[Authorize]
[Route("api/shopping-lists/current")]
public sealed class ShoppingListsController(
    ShoppingListService shoppingListService,
    ShoppingReceiptService shoppingReceiptService) : ControllerBase
{
    /// <summary>
    /// 現在の買い物リストを取得します。未作成の場合は空のリストを返します。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ShoppingListResponse>> GetCurrent(CancellationToken cancellationToken) =>
        Ok(await shoppingListService.GetCurrentAsync(cancellationToken));

    /// <summary>
    /// 現在の買い物リストから未購入項目の印刷用データを取得します。
    /// </summary>
    [HttpGet("print")]
    public async Task<ActionResult<ShoppingListPrintResponse>> GetPrintData(CancellationToken cancellationToken)
    {
        var response = await shoppingListService.GetPrintDataAsync(cancellationToken);
        return response is null
            ? NotFound(new ProblemDetails
            {
                Title = "印刷できる買い物リストが見つかりません。",
                Status = StatusCodes.Status404NotFound
            })
            : Ok(response);
    }

    /// <summary>
    /// 買い物リストへ項目を手動追加します。
    /// </summary>
    [HttpPost("items")]
    public async Task<ActionResult<ShoppingListResponse>> AddItem(
        AddShoppingItemRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return ValidationProblem("品名は必須です。");

        var response = await shoppingListService.AddItemAsync(request, name, cancellationToken);
        return response is null
            ? NotFound(new ProblemDetails
            {
                Title = "商品が見つかりません。",
                Status = StatusCodes.Status404NotFound
            })
            : Ok(response);
    }

    /// <summary>
    /// 補充点を下回った商品から買い物候補を作成・更新します。
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<ShoppingListResponse>> Generate(CancellationToken cancellationToken) =>
        Ok(await shoppingListService.GenerateSuggestionsAsync(cancellationToken));

    /// <summary>
    /// 買い物項目の数量または状態を更新します。
    /// </summary>
    [HttpPatch("items/{id:guid}")]
    public async Task<ActionResult<ShoppingListResponse>> UpdateItem(
        Guid id,
        UpdateShoppingItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Quantity.HasValue && !request.Status.HasValue)
            return ValidationProblem("数量または状態を指定してください。");

        var response = await shoppingListService.UpdateItemAsync(id, request, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// 現在の買い物リストを完了状態にします。
    /// </summary>
    [HttpPost("complete")]
    public async Task<ActionResult<ShoppingListResponse>> Complete(CancellationToken cancellationToken)
    {
        var response = await shoppingListService.CompleteAsync(cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// 完了したリストの購入済み商品を、指定した保管場所と期限の実在庫へ一括入庫します。
    /// </summary>
    [HttpPost("~/api/shopping-lists/{id:guid}/receive")]
    public async Task<ActionResult<ReceiveShoppingListResponse>> Receive(
        Guid id,
        ReceiveShoppingListRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var key = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Idempotency-Keyヘッダーは1〜100文字で指定してください。",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await shoppingReceiptService.ReceiveAsync(id, request, key, cancellationToken);
        return result.Status switch
        {
            ShoppingReceiptResultStatus.Success => Ok(new ReceiveShoppingListResponse(
                id,
                Domain.Shopping.ShoppingListStatus.Received,
                InventoryController.ToResponse(result.Operation!))),
            ShoppingReceiptResultStatus.NotFound => NotFound(new ProblemDetails
            {
                Title = "買い物リストまたは保管場所が見つかりません。",
                Status = StatusCodes.Status404NotFound
            }),
            ShoppingReceiptResultStatus.InvalidState => Conflict(new ProblemDetails
            {
                Title = "完了済みの買い物リストだけを入庫できます。",
                Status = StatusCodes.Status409Conflict
            }),
            ShoppingReceiptResultStatus.InvalidItems => BadRequest(new ProblemDetails
            {
                Title = "購入済みで商品に紐付く全項目の入庫先を、一度ずつ指定してください。",
                Status = StatusCodes.Status400BadRequest
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
