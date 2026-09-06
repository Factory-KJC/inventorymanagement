namespace InventoryAPI.Domain.Catalog;

/// <summary>
/// バーコードの検証を行うユーティリティクラス
/// </summary>
public static class BarcodeValidator
{
    public static bool IsValid(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return true;

        if ((barcode.Length != 8 && barcode.Length != 13) || barcode.Any(c => !char.IsAsciiDigit(c)))
            return false;

        var sum = 0;
        for (var index = 0; index < barcode.Length - 1; index++)
        {
            var digit = barcode[index] - '0';
            var weight = (barcode.Length - 2 - index) % 2 == 0 ? 3 : 1;
            sum += digit * weight;
        }

        var checkDigit = (10 - sum % 10) % 10;
        return checkDigit == barcode[^1] - '0';
    }
}
