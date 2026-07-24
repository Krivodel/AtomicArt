using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;

namespace AtomicArt.Desktop.Tests.Controls.Overlays;

public sealed class BackdropBlurImageFilterFactoryTests
{
    [Theory]
    [InlineData(8f, false)]
    [InlineData(12f, true)]
    [InlineData(40f, true)]
    public void UsesDownsampling_WithSigma_ReturnsExpectedResult(
        float sigma,
        bool expectedResult)
    {
        bool result =
            BackdropBlurImageFilterFactory.UsesDownsampling(sigma);

        result.Should().Be(expectedResult);
    }
}
