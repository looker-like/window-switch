using WindowSwitch.Controls;

namespace WindowSwitch.Tests;

public sealed class DesktopOverlayViewTests
{
    [Fact]
    public void RequiredContentHeightIncludesDesktopRowsAndActionButtons()
    {
        var height = DesktopOverlayView.CalculateRequiredContentHeight(
            desktopCount: 13,
            actionCount: 5,
            columns: 2);

        Assert.Equal(406, height);
    }

    [Fact]
    public void RequiredContentHeightNormalizesInvalidColumnCount()
    {
        var height = DesktopOverlayView.CalculateRequiredContentHeight(
            desktopCount: 2,
            actionCount: 0,
            columns: 0);

        Assert.Equal(100, height);
    }
}
