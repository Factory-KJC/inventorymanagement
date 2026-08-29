using System.Security.Cryptography;
using System.Text;
using InventoryAPI.Application.Printing;
using InventoryAPI.Contracts.Printing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers;

/// <summary>買い物リストの印刷要求、状態確認、同一内容の再印刷を提供します。</summary>
[ApiController]
[Authorize]
[Route("api/print-jobs")]
public sealed class PrintJobsController(PrintJobService service, IConfiguration configuration) : ControllerBase
{
    [HttpPost("~/api/shopping-lists/current/print-jobs")]
    public async Task<ActionResult<PrintJobResponse>> Create(CreatePrintJobRequest request, CancellationToken token)
    {
        var result = await service.CreateAsync(request.PaperWidth, token);
        return result is null ? NotFound(new ProblemDetails { Title = "印刷対象の買い物項目がありません。", Status = 404 }) : AcceptedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PrintJobResponse>> Get(Guid id, CancellationToken token) =>
        await service.GetAsync(id, token) is { } result ? Ok(result) : NotFound();

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<PrintJobResponse>> Retry(Guid id, CancellationToken token)
    {
        var result = await service.RetryAsync(id, token);
        return result is not null
            ? Accepted(result)
            : Conflict(new ProblemDetails
            {
                Title = $"処理中のジョブ、または試行上限（{PrintJobService.MaxAttempts}回）に達したジョブは再印刷できません。",
                Status = StatusCodes.Status409Conflict
            });
    }

    [AllowAnonymous]
    [HttpPost("worker/claim")]
    public async Task<ActionResult<PrintJobResponse>> Claim(CancellationToken token)
    {
        if (!IsWorker()) return Unauthorized();
        return await service.ClaimAsync(token) is { } result ? Ok(result) : NoContent();
    }

    [AllowAnonymous]
    [HttpPost("worker/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CompletePrintJobRequest request, CancellationToken token)
    {
        if (!IsWorker()) return Unauthorized();
        return await service.CompleteAsync(id, request.Error, token) ? NoContent() : NotFound();
    }

    private bool IsWorker()
    {
        var expected = configuration["PrintWorker:ApiKey"];
        var supplied = Request.Headers["X-Print-Worker-Key"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(expected) && supplied is not null &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }
}
