using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class AttachedImagePreparationPlannerTests
{
    [Fact]
    public void ShouldUseEncodingProbe_WithVeryHighPixelCount_ReturnsTrue()
    {
        AttachedImageCodecInfo imageInfo = new(
            10923,
            16383,
            SKAlphaType.Opaque);

        bool result = AttachedImagePreparationPlanner.ShouldUseEncodingProbe(imageInfo);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldUseEncodingProbe_WithModeratePixelCount_ReturnsFalse()
    {
        AttachedImageCodecInfo imageInfo = new(
            5504,
            3072,
            SKAlphaType.Opaque);

        bool result = AttachedImagePreparationPlanner.ShouldUseEncodingProbe(imageInfo);

        result.Should().BeFalse();
    }

    [Fact]
    public void EstimateEncodedBytes_WithScaledProbe_ExtrapolatesByPixelCount()
    {
        SKSizeI targetSize = new(4000, 2000);
        SKSizeI probeSize = new(1000, 500);

        long result = AttachedImagePreparationPlanner.EstimateEncodedBytes(
            targetSize,
            probeSize,
            100);

        result.Should().Be(1600);
    }
}
