using System.Globalization;
using System.Text;
using InventoryAPI.Contracts.Printing;
using InventoryAPI.Data;
using InventoryAPI.Domain;
using InventoryAPI.Domain.Printing;
using InventoryAPI.Domain.Shopping;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Application.Printing;

/// <summary>買い物リストを不変のreceiptline文書としてキューへ登録し、印刷状態を管理します。</summary>
public sealed class PrintJobService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public const int MaxAttempts = 3;

    public async Task<PrintJobResponse?> CreateAsync(PrintPaperWidth width, CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotAsync(cancellationToken);
        if (snapshot is null) return null;
        var now = timeProvider.GetUtcNow();
        var job = new PrintJob
        {
            Id = Guid.NewGuid(),
            HouseholdId = SystemDefaults.HouseholdId,
            ShoppingListId = snapshot.ShoppingListId,
            PaperWidth = width,
            ReceiptLine = snapshot.ReceiptLine,
            Status = PrintJobStatus.Pending,
            CreatedAt = now
        };
        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(job);
    }

    public async Task<PrintPreviewResponse?> GetPreviewAsync(PrintPaperWidth width, CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotAsync(cancellationToken);
        return snapshot is null ? null : new PrintPreviewResponse(width, snapshot.ReceiptLine);
    }

    private async Task<PrintSnapshot?> BuildSnapshotAsync(CancellationToken cancellationToken)
    {
        var list = await db.ShoppingLists.Include(x => x.Items).SingleOrDefaultAsync(x =>
            x.HouseholdId == SystemDefaults.HouseholdId && x.Status == ShoppingListStatus.Active, cancellationToken);
        if (list is null) return null;
        var pending = list.Items.Where(x => x.Status == ShoppingItemStatus.Pending).OrderBy(x => x.Name).ThenBy(x => x.Id).ToList();
        if (pending.Count == 0) return null;
        var productIds = pending.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToList();
        var units = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Unit, cancellationToken);
        var serverLocalNow = timeProvider.GetLocalNow();
        return new PrintSnapshot(list.Id, BuildDocument(pending, units, serverLocalNow));
    }

    public async Task<PrintJobResponse?> GetAsync(Guid id, CancellationToken token) =>
        (await db.PrintJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == SystemDefaults.HouseholdId, token)) is { } job ? ToResponse(job) : null;

    public async Task<PrintJobResponse?> RetryAsync(Guid id, CancellationToken token)
    {
        var job = await db.PrintJobs.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == SystemDefaults.HouseholdId, token);
        if (job is null || job.Status == PrintJobStatus.Processing || job.AttemptCount >= MaxAttempts) return null;
        job.Status = PrintJobStatus.Pending; job.LastError = null; job.StartedAt = null; job.CompletedAt = null;
        await db.SaveChangesAsync(token); return ToResponse(job);
    }

    public async Task<PrintJobResponse?> ClaimAsync(CancellationToken token)
    {
        var job = await db.PrintJobs.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(x => x.Status == PrintJobStatus.Pending, token);
        if (job is null) return null;
        job.Status = PrintJobStatus.Processing; job.AttemptCount++; job.StartedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(token); return ToResponse(job);
    }

    public async Task<bool> CompleteAsync(Guid id, string? error, CancellationToken token)
    {
        var job = await db.PrintJobs.SingleOrDefaultAsync(x => x.Id == id && x.Status == PrintJobStatus.Processing, token);
        if (job is null) return false;
        job.Status = error is null ? PrintJobStatus.Succeeded : PrintJobStatus.Failed;
        job.LastError = error; job.CompletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(token); return true;
    }

    private static string BuildDocument(IEnumerable<ShoppingListItem> items, IReadOnlyDictionary<Guid, string> units, DateTimeOffset now)
    {
        var japaneseCulture = CultureInfo.GetCultureInfo("ja-JP");
        var dayOfWeek = japaneseCulture.DateTimeFormat.GetAbbreviatedDayName(now.DayOfWeek);
        var b = new StringBuilder("^^^買い物リスト^^^\n-\n");
        b.Append(now.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture))
            .Append('（')
            .Append(dayOfWeek)
            .Append("） ")
            .Append(now.ToString("HH:mm", CultureInfo.InvariantCulture))
            .Append("\n-\n");
        foreach (var item in items)
        {
            var unit = item.ProductId.HasValue ? units.GetValueOrDefault(item.ProductId.Value) : null;
            b.Append(Escape(item.Name)).Append(" | ").Append(item.Quantity.ToString("0.####", CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(unit)) b.Append(' ').Append(Escape(unit));
            b.Append("\n-\n");
        }
        return b.Append("^Home Stock^").ToString();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal).Replace("{", "\\{", StringComparison.Ordinal).Replace("^", "\\^", StringComparison.Ordinal);
    private static PrintJobResponse ToResponse(PrintJob x) => new(x.Id, x.ShoppingListId, x.PaperWidth, x.ReceiptLine, x.Status, x.AttemptCount, x.LastError, x.CreatedAt, x.StartedAt, x.CompletedAt);
    private sealed record PrintSnapshot(Guid ShoppingListId, string ReceiptLine);
}
