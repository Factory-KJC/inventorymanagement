using System.Threading.Channels;

namespace InventoryAPI.Application.Printing;

/// <summary>
/// 印刷ジョブの追加を接続中のWorkerへ通知します。通知はキューの内容ではなく、再取得が必要という合図だけを表します。
/// </summary>
public sealed class PrintJobNotifier
{
    private readonly Channel<bool> _signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });

    /// <summary>未処理ジョブが存在する可能性をWorkerへ通知します。</summary>
    public void Notify() => _signals.Writer.TryWrite(true);

    /// <summary>Workerの切断まで、ジョブ発生通知を非同期に列挙します。</summary>
    public IAsyncEnumerable<bool> ReadAllAsync(CancellationToken cancellationToken) =>
        _signals.Reader.ReadAllAsync(cancellationToken);
}
