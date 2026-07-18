using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class AttachedImageSignatureValidatorTests
{
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
            "image/jpeg" => [0xFF, 0xD8, 0xFF, 0x00],
            "image/png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00],
            "image/webp" => [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50],
            "image/heic" => [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63],
            "image/heif" => [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x69, 0x66, 0x31],
            _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported content type.")
        };
    }
}
