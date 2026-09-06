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
public sealed class PrintJobsController(
    PrintJobService service,
    PrintJobNotifier notifier,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("~/api/shopping-lists/current/print-preview")]
    public async Task<ActionResult<PrintPreviewResponse>> Preview(
        [FromQuery] Domain.Printing.PrintPaperWidth paperWidth = Domain.Printing.PrintPaperWidth.Mm80,
        CancellationToken token = default)
    {
        var result = await service.GetPreviewAsync(paperWidth, token);
        return result is null
            ? NotFound(new ProblemDetails { Title = "印刷対象の買い物項目がありません。", Status = 404 })
            : Ok(result);
    }

    [HttpPost("~/api/shopping-lists/current/print-jobs")]
    public async Task<ActionResult<PrintJobResponse>> Create(CreatePrintJobRequest request, CancellationToken token)
    {
        var result = await service.CreateAsync(request.PaperWidth, token);
        if (result is not null) notifier.Notify();
        return result is null ? NotFound(new ProblemDetails { Title = "印刷対象の買い物項目がありません。", Status = 404 }) : AcceptedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PrintJobResponse>> Get(Guid id, CancellationToken token) =>
        await service.GetAsync(id, token) is { } result ? Ok(result) : NotFound();

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<PrintJobResponse>> Retry(Guid id, CancellationToken token)
    {
        var result = await service.RetryAsync(id, token);
        if (result is not null) notifier.Notify();
        return result is not null
            ? Accepted(result)
            : Conflict(new ProblemDetails
            {
                Title = $"処理中のジョブ、または試行上限（{PrintJobService.MaxAttempts}回）に達したジョブは再印刷できません。",
                Status = StatusCodes.Status409Conflict
            });
    }

    [AllowAnonymous]
    [HttpGet("worker/events")]
    public async Task Events(CancellationToken token)
    {
        if (!IsWorker())
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        await WriteSignalAsync(token);
        await foreach (var _ in notifier.ReadAllAsync(token))
        {
            await WriteSignalAsync(token);
        }
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

    private async Task WriteSignalAsync(CancellationToken token)
    {
        await Response.WriteAsync("event: print-job\ndata: available\n\n", token);
        await Response.Body.FlushAsync(token);
    }
}
