using System.Globalization;
using System.Text;
using HomeStock.Windows.Models;

namespace HomeStock.Windows.Services;

/// <summary>ReceiptLine文書を画像化せず、用紙幅に合わせた読み取り専用テキストプレビューへ整形します。</summary>
public static class ReceiptPreviewFormatter
{
    public static string Format(string receiptLine, PrintPaperWidth paperWidth)
    {
        var charactersPerLine = paperWidth == PrintPaperWidth.Mm80 ? 48 : 32;
        var result = new StringBuilder();
        foreach (var sourceLine in receiptLine.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = sourceLine.TrimEnd();
            if (line == "-")
            {
                result.AppendLine(new string('─', charactersPerLine));
                continue;
            }

            if (line.StartsWith('^') && line.EndsWith('^'))
            {
                result.AppendLine(Center(line.Trim('^'), charactersPerLine));
                continue;
            }

            var columns = line.Split(" | ", 2, StringSplitOptions.None);
            result.AppendLine(columns.Length == 2
                ? FormatColumns(columns[0], columns[1], charactersPerLine)
                : Center(line, charactersPerLine));
        }

        return result.ToString().TrimEnd();
    }

    private static string FormatColumns(string left, string right, int width)
    {
        var padding = Math.Max(1, width - DisplayWidth(left) - DisplayWidth(right));
        return left + new string(' ', padding) + right;
    }

    private static string Center(string value, int width)
    {
        var padding = Math.Max(0, (width - DisplayWidth(value)) / 2);
        return new string(' ', padding) + value;
    }

    private static int DisplayWidth(string value) => value.Sum(character =>
        CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.OtherLetter ? 2 : 1);
}
