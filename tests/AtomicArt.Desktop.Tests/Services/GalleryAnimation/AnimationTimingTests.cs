using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.GalleryAnimation;

public sealed class AnimationTimingTests
{
    [Theory]
    [InlineData(0.10d, 0.50d)]
    [InlineData(0.50d, 0.50d)]
    [InlineData(1.25d, 1.25d)]
    [InlineData(2.00d, 2.00d)]
    [InlineData(3.00d, 2.00d)]
    public void ClampSpeed_WithValueOutsideReferenceRange_ReturnsClampedSpeed(
        double value,
        double expected)
    {
        double result = AnimationTiming.ClampSpeed(value);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-10d, 0)]
    [InlineData(0d, 0)]
    [InlineData(420.4d, 420)]
    [InlineData(420.5d, 420)]
    [InlineData(420.6d, 421)]
    [InlineData(1500d, 1000)]
    public void ClampDelay_WithValueOutsideReferenceRange_ReturnsClampedDelay(
        double value,
        int expected)
    {
        int result = AnimationTiming.ClampDelay(value);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(360, 2.00d, 180)]
    [InlineData(360, 1.00d, 360)]
    [InlineData(360, 0.50d, 720)]
    [InlineData(360, 0.10d, 720)]
    [InlineData(360, 3.00d, 180)]
    public void ScaleTime_WithPositiveMilliseconds_ReturnsMillisecondsDividedByClampedSpeed(
        int milliseconds,
        double speed,
        int expected)
    {
        int result = AnimationTiming.ScaleTime(milliseconds, speed);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ScaleTime_WithNonPositiveMilliseconds_ReturnsZero(int milliseconds)
    {
        int result = AnimationTiming.ScaleTime(milliseconds, 1d);

        result.Should().Be(0);
    }
}
