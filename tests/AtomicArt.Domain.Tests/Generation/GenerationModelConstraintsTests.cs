using FluentAssertions;
using Xunit;

using AtomicArt.Domain.Exceptions;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Domain.Tests.Generation;

public sealed class GenerationModelConstraintsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidModelId_ThrowsDomainException(string? modelId)
    {
        Action action = () => CreateConstraints(modelId: modelId!);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-100");
    }

    [Fact]
    public void Constructor_WithNonPositiveLimit_ThrowsDomainException()
    {
        Action action = () => CreateConstraints(maxPromptLength: 0);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-101");
    }

    [Fact]
    public void Constructor_WithMissingRequiredList_ThrowsDomainException()
    {
        Action action = () => CreateConstraints(aspectRatios: []);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-102");
    }

    [Fact]
    public void Constructor_WithEmptyListItem_ThrowsDomainException()
    {
        Action action = () => CreateConstraints(aspectRatios: ["Auto", " "]);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-102");
    }

    [Fact]
    public void Constructor_WithNonPositiveGenerationCount_ThrowsDomainException()
    {
        Action action = () => CreateConstraints(generationCounts: [1, 0]);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-101");
    }

    [Fact]
    public void Constructor_WithTotalAttachmentLimitBelowSingleLimit_ThrowsDomainException()
    {
        Action action = () => CreateConstraints(
            maxAttachedImageBytes: 2_048,
            maxTotalAttachedImageBytes: 1_024);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("ERR-GEN-103");
    }

    private static GenerationModelConstraints CreateConstraints(
        string modelId = "test-model",
        int maxPromptLength = 100,
        IReadOnlyList<string>? aspectRatios = null,
        IReadOnlyList<string>? resolutions = null,
        IReadOnlyList<int>? generationCounts = null,
        int maxAttachedImages = 1,
        long maxAttachedImageBytes = 1_024,
        long maxTotalAttachedImageBytes = 2_048,
        IReadOnlyList<string>? supportedContentTypes = null)
    {
        return new GenerationModelConstraints(
            modelId,
            maxPromptLength,
            aspectRatios ?? ["Auto"],
            resolutions ?? ["1k"],
            generationCounts ?? [1],
            new GenerationModelTemperatureConstraints(0.1d, 2d, 1d, 0.1d),
            maxAttachedImages,
            maxAttachedImageBytes,
            maxTotalAttachedImageBytes,
            supportedContentTypes ?? ["image/png"]);
    }
}
