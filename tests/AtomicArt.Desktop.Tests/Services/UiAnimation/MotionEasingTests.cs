using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.UiAnimation;

public sealed class MotionEasingTests
{
    public static TheoryData<Func<double, double>, double, double> ReferenceSamples =>
        new TheoryData<Func<double, double>, double, double>
        {
            { MotionEasing.EaseOut, 0d, 0d },
            { MotionEasing.EaseOut, 0.25d, 0.578125d },
            { MotionEasing.EaseOut, 0.5d, 0.875d },
            { MotionEasing.EaseOut, 0.75d, 0.984375d },
            { MotionEasing.EaseOut, 1d, 1d },
            { MotionEasing.EaseRail, 0d, 0d },
            { MotionEasing.EaseRail, 0.25d, 0.44565392967742323d },
            { MotionEasing.EaseRail, 0.5d, 0.81900414413722489d },
            { MotionEasing.EaseRail, 0.75d, 0.96352085669164911d },
            { MotionEasing.EaseRail, 1d, 1d },
            { MotionEasing.EaseMaterial, 0d, 0d },
            { MotionEasing.EaseMaterial, 0.25d, 0.812131184615433d },
            { MotionEasing.EaseMaterial, 0.5d, 0.95683848994775467d },
            { MotionEasing.EaseMaterial, 0.75d, 0.99303416534723565d },
            { MotionEasing.EaseMaterial, 1d, 1d }
        };

    [Theory]
    [MemberData(nameof(ReferenceSamples))]
    public void Easing_WithReferenceSample_ReturnsExpectedValue(
        Func<double, double> easing,
        double value,
        double expected)
    {
        double result = easing(value);

        result.Should().BeApproximately(expected, 0.000000000001d);
    }
}
