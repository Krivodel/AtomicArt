using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Services;

namespace AtomicArt.Application.Tests.Features.Generation.Services;

public sealed class GenerationUsageModalityNormalizerTests
{
    [Theory]
    [InlineData(" IMAGE ", "image")]
    [InlineData("TeXt", "text")]
    [InlineData(" AUDIO ", "audio")]
    public void Normalize_WithWhitespaceAndMixedCase_ReturnsCanonicalValue(
        string modality,
        string expected)
    {
        string result = GenerationUsageModalityNormalizer.Normalize(modality);

        result.Should().Be(expected);
    }
}
