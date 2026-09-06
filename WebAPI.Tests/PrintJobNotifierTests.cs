using InventoryAPI.Application.Printing;

namespace InventoryAPI.Tests;

public sealed class PrintJobNotifierTests
{
    [Fact]
    public async Task Notify_MakesSignalAvailable()
    {
        var notifier = new PrintJobNotifier();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        notifier.Notify();
        await using var signals = notifier.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);

        Assert.True(await signals.MoveNextAsync());
        Assert.True(signals.Current);
    }

    [Fact]
    public async Task Notify_WhenSignalIsPending_CoalescesNotifications()
    {
        var notifier = new PrintJobNotifier();

        notifier.Notify();
        notifier.Notify();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var signals = notifier.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.True(await signals.MoveNextAsync());
        var nextSignal = signals.MoveNextAsync().AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        Assert.False(nextSignal.IsCompleted);

        notifier.Notify();
        Assert.True(await nextSignal);
    }
}
