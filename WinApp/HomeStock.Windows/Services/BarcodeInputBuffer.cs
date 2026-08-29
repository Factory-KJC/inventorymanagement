namespace HomeStock.Windows.Services;

/// <summary>USB-HIDスキャナのキー入力をEnterまで一つのJANコードとして集約します。</summary>
public sealed class BarcodeInputBuffer(TimeProvider timeProvider, TimeSpan? resetAfter = null)
{
    private readonly TimeSpan _resetAfter = resetAfter ?? TimeSpan.FromMilliseconds(250);
    private readonly List<char> _characters = [];
    private DateTimeOffset _lastInputAt;

    public string? Push(char character)
    {
        var now = timeProvider.GetUtcNow();
        if (_characters.Count > 0 && now - _lastInputAt > _resetAfter)
        {
            _characters.Clear();
        }

        _lastInputAt = now;
        if (character is '\r' or '\n')
        {
            var barcode = new string([.. _characters]);
            _characters.Clear();
            return barcode.Length is 8 or 13 && barcode.All(char.IsAsciiDigit) ? barcode : null;
        }

        if (char.IsAsciiDigit(character))
        {
            _characters.Add(character);
        }
        else
        {
            _characters.Clear();
        }

        return null;
    }
}
