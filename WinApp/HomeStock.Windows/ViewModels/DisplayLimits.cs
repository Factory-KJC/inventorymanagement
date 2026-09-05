namespace HomeStock.Windows.ViewModels;

/// <summary>クライアント画面へ一度に描画するレコード数を制限します。</summary>
public static class DisplayLimits
{
    public const int MaximumRecords = 20;

    /// <summary>元の並び順を維持して、画面表示可能な件数だけを返します。</summary>
    public static IEnumerable<T> Apply<T>(IEnumerable<T> source) => source.Take(MaximumRecords);
}
