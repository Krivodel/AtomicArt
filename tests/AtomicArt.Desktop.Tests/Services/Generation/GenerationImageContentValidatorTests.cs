using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationImageContentValidatorTests
{
    private const int KnownLargePlaceholderBytes = 20_583_803;

    private readonly GenerationImageContentValidator _validator =
        GenerationImageFormatRegistryTestFactory.CreateValidator();

    [Fact]
    public void TryValidate_WithValidPng_ReturnsBytes()
    {
        byte[] validPngBytes = GenerationImageTestData.ValidPngBytes;
        GenerationImageContentDto content = new("image/png", Convert.ToBase64String(validPngBytes));

        bool result = _validator.TryValidate(content, out GenerationImageContentValidationResult? validationResult);

        result.Should().BeTrue();
        validationResult.Should().NotBeNull();
        GenerationImageContentValidationResult validatedContent = validationResult
            ?? throw new InvalidOperationException("Validated image content is required.");
        validatedContent.ContentType.Should().Be("image/png");
        validatedContent.Bytes.ToArray().Should().Equal(validPngBytes);
    }

    [Fact]
    public void TryValidate_WithUnsupportedContentType_ReturnsFalse()
    {
        GenerationImageContentDto content = new(
            "image/gif",
            Convert.ToBase64String(GenerationImageTestData.ValidPngBytes));

        AssertRejected(content);
    }

    [Fact]
    public void TryValidate_WithOversizedBase64_ReturnsFalse()
    {
        GenerationImageContentValidator validator = GenerationImageFormatRegistryTestFactory.CreateValidator(
            GenerationImageTestData.TestMaxImageBytes);
        GenerationImageContentDto content = new(
            "image/png",
            new string('A', GenerationImageTestData.TestOversizedBase64Length));

        AssertRejected(content, validator);
    }

    [Fact]
    public void DefaultMaxImageBytes_WithKnownLargePlaceholders_CoversCurrentResources()
    {
        GenerationImageContentValidator.DefaultMaxImageBytes.Should().Be(128 * 1_048_576);
        KnownLargePlaceholderBytes.Should().BeLessThan(GenerationImageContentValidator.DefaultMaxImageBytes);
    }

    [Fact]
    public void TryValidate_WithInvalidBase64_ReturnsFalse()
    {
        GenerationImageContentDto content = new("image/png", "not-base64");

        AssertRejected(content);
    }

    [Fact]
    public void TryValidate_WithInvalidSignature_ReturnsFalse()
    {
        GenerationImageContentDto content = new("image/png", Convert.ToBase64String([0x01, 0x02, 0x03]));

        AssertRejected(content);
    }

    [Fact]
    public void TryValidate_WithShortWebpHeader_ReturnsFalse()
    {
        GenerationImageContentDto content = new(
            "image/webp",
            Convert.ToBase64String(GenerationImageFileSignatures.Riff.ToArray()));

        AssertRejected(content);
    }

    private void AssertRejected(
        GenerationImageContentDto content,
        GenerationImageContentValidator? validator = null)
    {
        bool result = (validator ?? _validator).TryValidate(
            content,
            out GenerationImageContentValidationResult? validationResult);

        result.Should().BeFalse();
        validationResult.Should().BeNull();
    }
}
