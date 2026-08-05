using InventoryAPI.Application.Shopping;
using InventoryAPI.Contracts.Shopping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers;

/// <summary>
/// 現在有効な買い物リストを操作します。
/// </summary>
[ApiController]
[Authorize]
[Route("api/shopping-lists/current")]
public sealed class ShoppingListsController(ShoppingListService shoppingListService) : ControllerBase
{
    /// <summary>
    /// 現在の買い物リストを取得します。未作成の場合は空のリストを返します。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ShoppingListResponse>> GetCurrent(CancellationToken cancellationToken) =>
        Ok(await shoppingListService.GetCurrentAsync(cancellationToken));

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
}
