using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationImageFileNamePolicyTests
{
    private static readonly Guid BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherItemId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void BuildFileName_WithValidIds_ReturnsGenerationFileName()
    {
        GenerationImageFileNamePolicy policy = new();

        string fileName = policy.BuildFileName(BatchId, ItemId, ".png");

        fileName.Should().Be(
            "generation-22222222222222222222222222222222-11111111111111111111111111111111.png");
    }

    [Fact]
    public void BuildFileName_WithExtensionWithoutDot_AddsDot()
    {
        GenerationImageFileNamePolicy policy = new();

        string fileName = policy.BuildFileName(BatchId, ItemId, "png");

        fileName.Should().Be(
            "generation-22222222222222222222222222222222-11111111111111111111111111111111.png");
    }

    [Fact]
    public void BuildFileName_WithEmptyExtension_ThrowsArgumentException()
    {
        GenerationImageFileNamePolicy policy = new();

        Action act = () => policy.BuildFileName(BatchId, ItemId, string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsFileNameForItem_WithMatchingGeneratedFileName_ReturnsTrue()
    {
        GenerationImageFileNamePolicy policy = new();
        string fileName = policy.BuildFileName(BatchId, ItemId, ".png");

        bool result = policy.IsFileNameForItem(fileName, ItemId);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsFileNameForItem_WithDifferentItemId_ReturnsFalse()
    {
        GenerationImageFileNamePolicy policy = new();
        string fileName = policy.BuildFileName(BatchId, OtherItemId, ".png");

        bool result = policy.IsFileNameForItem(fileName, ItemId);

        result.Should().BeFalse();
    }
}
