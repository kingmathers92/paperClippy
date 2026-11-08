using PaperClippy.Core.Mascot;
using Xunit;

namespace PaperClippy.Core.Tests.Mascot;

public class MascotPlacementTests
{
    [Fact]
    public void Defaults_AreVisible_Unplaced_AtNormalSize()
    {
        var p = new MascotPlacement();
        Assert.Null(p.X);
        Assert.Null(p.Y);
        Assert.True(p.Visible);
        Assert.Equal(1.0, p.Scale);
    }

    [Theory]
    [InlineData(0.01, MascotPlacement.MinScale)]
    [InlineData(99.0, MascotPlacement.MaxScale)]
    [InlineData(1.5, 1.5)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.NegativeInfinity, 1.0)]
    public void Sanitized_KeepsScaleInSupportedRange(double input, double expected)
    {
        Assert.Equal(expected, new MascotPlacement(Scale: input).Sanitized().Scale);
    }

    [Fact]
    public void Sanitized_DropsAnUnusablePosition()
    {
        Assert.Null(new MascotPlacement(X: 10).Sanitized().X);
        Assert.Null(new MascotPlacement(X: double.NaN, Y: 5).Sanitized().Y);

        var ok = new MascotPlacement(X: 10, Y: -20).Sanitized();
        Assert.Equal(10.0, ok.X);
        Assert.Equal(-20.0, ok.Y);
    }
}
