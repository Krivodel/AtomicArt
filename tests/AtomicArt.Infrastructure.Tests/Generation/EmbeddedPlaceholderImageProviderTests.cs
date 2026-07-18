using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation;

public sealed class EmbeddedPlaceholderImageProviderTests
{
    [Theory]
    [InlineData("placeholder.jpg", GenerationImageContentTypes.Jpeg)]
    [InlineData("placeholder.jpeg", GenerationImageContentTypes.Jpeg)]
    [InlineData("placeholder.png", GenerationImageContentTypes.Png)]
    [InlineData("placeholder.webp", GenerationImageContentTypes.Webp)]
    [InlineData("placeholder.JPG", GenerationImageContentTypes.Jpeg)]
    public void GetContentType_WithSupportedExtension_ReturnsCatalogContentType(
        string resourceName,
        string expectedContentType)
    {
        string contentType = EmbeddedPlaceholderImageProvider.GetContentType(resourceName);

        contentType.Should().Be(expectedContentType);
    }

    [Fact]
    public void GetContentType_WithUnsupportedCatalogExtension_ThrowsInvalidOperationException()
    {
        Func<string> act = () => EmbeddedPlaceholderImageProvider.GetContentType("placeholder.gif");

        act.Should()
            .Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task ReadResourceContentAsync_WhenResourceExceedsLimit_ThrowsInvalidOperationException()
    {
        Mock<Stream> stream = new Mock<Stream>();
        stream.SetupGet(currentStream => currentStream.CanSeek)
            .Returns(true);
        stream.SetupGet(currentStream => currentStream.Length)
            .Returns(EmbeddedPlaceholderImageProvider.MaxPlaceholderImageBytes + 1L);

        Func<Task> act = () => EmbeddedPlaceholderImageProvider
            .ReadResourceContentAsync(stream.Object, "large-placeholder.png", CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{EmbeddedPlaceholderImageProvider.MaxPlaceholderImageMegabytes} MB*");
    }
}
