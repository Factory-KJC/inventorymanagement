using HomeStock.Windows.Services;

namespace HomeStock.Windows.Tests;

public sealed class BarcodeInputBufferTests
{
    [Fact]
    public void Push_ThirteenDigitsAndEnter_ReturnsBarcode()
    {
        var buffer = new BarcodeInputBuffer(TimeProvider.System);

        string? result = null;
        foreach (var character in "4901234567894\r")
        {
            result = buffer.Push(character) ?? result;
        }

        Assert.Equal("4901234567894", result);
    }

    [Fact]
    public void Push_NonDigitInSequence_DiscardsPartialInput()
    {
        var buffer = new BarcodeInputBuffer(TimeProvider.System);

        foreach (var character in "4901A2345678\r")
        {
            buffer.Push(character);
        }

        Assert.Null(buffer.Push('\r'));
    }
}
