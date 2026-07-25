namespace TarkovHelper.Tests;

/// <summary>
/// Width→mode mapping for the main-window header's narrow-width degradation
/// (top-bar redesign): Full ≥ 1000, Compact ≥ 760, Minimal below that.
/// </summary>
public class HeaderLayoutTests
{
    [Theory]
    [InlineData(300, HeaderLayoutMode.Minimal)]   // far below the window minimum
    [InlineData(599, HeaderLayoutMode.Minimal)]
    [InlineData(600, HeaderLayoutMode.Minimal)]   // window MinWidth
    [InlineData(759, HeaderLayoutMode.Minimal)]   // boundary: last Minimal width
    [InlineData(760, HeaderLayoutMode.Compact)]   // boundary: first Compact width
    [InlineData(999, HeaderLayoutMode.Compact)]   // boundary: last Compact width
    [InlineData(1000, HeaderLayoutMode.Full)]     // boundary: first Full width
    [InlineData(1400, HeaderLayoutMode.Full)]     // default window width
    [InlineData(3840, HeaderLayoutMode.Full)]
    public void GetMode_maps_width_to_mode(double width, HeaderLayoutMode expected)
        => Assert.Equal(expected, HeaderLayout.GetMode(width));

    [Fact]
    public void Thresholds_are_ordered()
        => Assert.True(HeaderLayout.MinimalThreshold < HeaderLayout.CompactThreshold);
}
