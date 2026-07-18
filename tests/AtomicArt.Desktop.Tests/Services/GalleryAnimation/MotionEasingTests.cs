using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.GalleryAnimation;

public sealed class MotionEasingTests
{
    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(0.25d, 0.578125d)]
    [InlineData(0.5d, 0.875d)]
    [InlineData(0.75d, 0.984375d)]
    [InlineData(1d, 1d)]
    public void EaseOut_WithReferenceSample_ReturnsCubicEaseOut(
        double value,
        double expected)
    {
        double result = MotionEasing.EaseOut(value);

        result.Should().BeApproximately(expected, 0.000000000001d);
    }

    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(0.25d, 0.44565392967742323d)]
    [InlineData(0.5d, 0.81900414413722489d)]
    [InlineData(0.75d, 0.96352085669164911d)]
    [InlineData(1d, 1d)]
    public void EaseRail_WithReferenceSample_ReturnsReferenceCubicBezier(
        double value,
        double expected)
    {
        double result = MotionEasing.EaseRail(value);

        result.Should().BeApproximately(expected, 0.000000000001d);
    }

    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(0.25d, 0.812131184615433d)]
    [InlineData(0.5d, 0.95683848994775467d)]
    [InlineData(0.75d, 0.99303416534723565d)]
    [InlineData(1d, 1d)]
    public void EaseMaterial_WithReferenceSample_ReturnsReferenceCubicBezier(
        double value,
        double expected)
    {
        double result = MotionEasing.EaseMaterial(value);

        result.Should().BeApproximately(expected, 0.000000000001d);
    }
}
