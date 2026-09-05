using HomeStock.Windows.ViewModels;

namespace HomeStock.Windows.Tests;

public sealed class DisplayLimitsTests
{
    [Fact]
    public void Apply_MoreThanMaximumRecords_ReturnsOnlyMaximumRecordsInOriginalOrder()
    {
        var records = Enumerable.Range(1, DisplayLimits.MaximumRecords + 5);

        var displayed = DisplayLimits.Apply(records).ToList();

        Assert.Equal(DisplayLimits.MaximumRecords, displayed.Count);
        Assert.Equal(Enumerable.Range(1, DisplayLimits.MaximumRecords), displayed);
    }
}
