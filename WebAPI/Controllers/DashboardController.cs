using InventoryAPI.Application.Dashboard;
using InventoryAPI.Contracts.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers;

/// <summary>
/// ホーム画面に表示する集計値と、その内訳一覧を提供します。
/// </summary>
[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardService dashboardService) : ControllerBase
{
    /// <summary>
    /// 登録商品、在庫不足、期限間近、買い物項目の件数を取得します。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> GetSummary(CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetSummaryAsync(cancellationToken));

    /// <summary>
    /// 指定カテゴリの内訳を取得します。1ページの最大件数は20件です。
    /// </summary>
    [HttpGet("{category}")]
    public async Task<ActionResult<DashboardListResponse>> GetList(
        string category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DashboardService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var response = await dashboardService.GetListAsync(category, page, pageSize, cancellationToken);
        return response is null
            ? NotFound(new ProblemDetails
            {
                Title = "ダッシュボード項目が見つかりません。",
                Status = StatusCodes.Status404NotFound
            })
            : Ok(response);
    }
}
