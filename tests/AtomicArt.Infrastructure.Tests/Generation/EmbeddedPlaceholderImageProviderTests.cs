using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation;

public sealed class EmbeddedPlaceholderImageProviderTests
{
    private const long MaxPlaceholderImageBytes = 128L * 1024L * 1024L;

    [Fact]
    public async Task ReadResourceContentAsync_WhenResourceExceedsLimit_ThrowsInvalidOperationException()
    {
        Mock<Stream> stream = new Mock<Stream>();
        stream.SetupGet(currentStream => currentStream.CanSeek)
            .Returns(true);
        stream.SetupGet(currentStream => currentStream.Length)
            .Returns(MaxPlaceholderImageBytes + 1L);

        Func<Task> act = () => EmbeddedPlaceholderImageProvider
            .ReadResourceContentAsync(stream.Object, "large-placeholder.png", CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*128 MB*");
    }
}
