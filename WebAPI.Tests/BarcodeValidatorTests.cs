using InventoryAPI.Domain.Catalog;

namespace InventoryAPI.Tests;

public sealed class BarcodeValidatorTests
{
    [Theory]
    [InlineData("4901234567894")]
    [InlineData("96385074")]
    [InlineData(null)]
    [InlineData("")]
    public void IsValid_AcceptsValidOrEmptyBarcode(string? barcode) =>
        Assert.True(BarcodeValidator.IsValid(barcode));

    [Theory]
    [InlineData("4901234567890")]
    [InlineData("123")]
    [InlineData("ABCDEFGHIJKLM")]
    public void IsValid_RejectsInvalidBarcode(string barcode) =>
        Assert.False(BarcodeValidator.IsValid(barcode));
}
