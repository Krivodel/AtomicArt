using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class AttachedImageSignatureValidatorTests
{
    private const byte HeifBoxLength = 0x18;

    [Theory]
    [InlineData("image.jpg", "image/jpeg")]
    [InlineData("image.jpeg", "image/jpeg")]
    [InlineData("image.png", "image/png")]
    [InlineData("image.webp", "image/webp")]
    [InlineData("image.heic", "image/heic")]
    [InlineData("image.heif", "image/heif")]
    public void TryGetContentType_WithMatchingSignature_ReturnsContentType(
        string fileName,
        string expectedContentType)
    {
        AttachedImageSignatureValidator validator = new();
        byte[] content = CreateContent(expectedContentType);

        bool result = validator.TryGetContentType(fileName, content, out string contentType);

        result.Should().BeTrue();
        contentType.Should().Be(expectedContentType);
    }

    [Fact]
    public void TryGetContentType_WithMismatchedSignature_ReturnsFalse()
    {
        AttachedImageSignatureValidator validator = new();
        byte[] content = [0x00, 0x01, 0x02, 0x03];

        bool result = validator.TryGetContentType("image.png", content, out string contentType);

        result.Should().BeFalse();
        contentType.Should().BeEmpty();
    }

    private static byte[] CreateContent(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => CreateContent(GenerationImageFileSignatures.Jpeg, 4),
            "image/png" => CreateContent(GenerationImageFileSignatures.Png, 9),
            "image/webp" => CreateWebpContent(),
            "image/heic" => CreateHeifContent(GenerationImageFileSignatures.HeicBrand),
            "image/heif" => CreateHeifContent(GenerationImageFileSignatures.Mif1Brand),
            _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported content type.")
        };
    }

    private static byte[] CreateContent(ReadOnlySpan<byte> signature, int length)
    {
        byte[] content = new byte[length];
        signature.CopyTo(content);

        return content;
    }

    private static byte[] CreateWebpContent()
    {
        byte[] content = new byte[12];
        GenerationImageFileSignatures.Riff.CopyTo(content);
        GenerationImageFileSignatures.Webp.CopyTo(
            content.AsSpan(GenerationImageFileSignatures.WebpFormatOffset));

        return content;
    }

    private static byte[] CreateHeifContent(ReadOnlySpan<byte> brand)
    {
        byte[] content = new byte[12];
        content[3] = HeifBoxLength;
        GenerationImageFileSignatures.Ftyp.CopyTo(content.AsSpan(4));
        brand.CopyTo(content.AsSpan(GenerationImageFileSignatures.HeifBrandOffset));

        return content;
    }
}
