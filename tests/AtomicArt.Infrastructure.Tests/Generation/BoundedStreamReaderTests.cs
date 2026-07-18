using FluentAssertions;
using Xunit;

using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation;

public sealed class BoundedStreamReaderTests
{
    [Fact]
    public async Task ReadToEndAsync_WithContentAtLimit_ReturnsContent()
    {
        byte[] expectedContent = [1, 2, 3];
        using MemoryStream stream = new(expectedContent);

        byte[] content = await BoundedStreamReader.ReadToEndAsync(
            stream,
            expectedContent.LongLength,
            CreateTooLargeException,
            CancellationToken.None);

        content.Should().Equal(expectedContent);
    }

    [Fact]
    public async Task ReadToEndAsync_WhenContentExceedsLimit_ThrowsConfiguredException()
    {
        byte[] sourceContent = [1, 2, 3];
        using MemoryStream stream = new(sourceContent);

        Func<Task> act = () => BoundedStreamReader.ReadToEndAsync(
            stream,
            sourceContent.LongLength - 1L,
            CreateTooLargeException,
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Configured size limit was exceeded.");
    }

    private static InvalidOperationException CreateTooLargeException()
    {
        return new InvalidOperationException("Configured size limit was exceeded.");
    }
}
