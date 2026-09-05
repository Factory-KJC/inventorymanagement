using HomeStock.Windows.Models;
using HomeStock.Windows.Services;

namespace HomeStock.Windows.Tests;

public sealed class ReceiptPreviewFormatterTests
{
    [Fact]
    public void Format_RendersRulesDecorationsAndColumnsAsTextPreview()
    {
        const string receiptLine = "-\n^^^買い物リスト^^^\n-\n洗剤 | 2 本\n-\n^Home Stock^";

        var result = ReceiptPreviewFormatter.Format(receiptLine, PrintPaperWidth.Mm80);

        var lines = result.Split(Environment.NewLine);
        Assert.Equal(new string('─', 48), lines[0]);
        Assert.Equal("買い物リスト", lines[1].Trim());
        Assert.StartsWith("洗剤", lines[3]);
        Assert.EndsWith("2 本", lines[3]);
        Assert.Equal("Home Stock", lines[5].Trim());
    }
}
