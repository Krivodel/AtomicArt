using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class TransferredImageFileNameTests
{
    [Theory]
    [InlineData("", "fallback.png")]
    [InlineData(".", "fallback.png")]
    [InlineData("..", "fallback.png")]
    public void Sanitize_WithUnsafeName_ReturnsFallback(
        string candidate,
        string expected)
    {
        string result = TransferredImageFileName.Sanitize(
            candidate,
            "fallback.png",
            TestApiConfiguration
                .CreateDataTransferOptions()
                .MaximumTransferredFileNameCharacters);

        result.Should().Be(expected);
    }

    [Fact]
    public void Sanitize_WithLongName_LimitsLength()
    {
        string candidate = new('a', 256);

        string result = TransferredImageFileName.Sanitize(
            candidate,
            "fallback.png",
            TestApiConfiguration
                .CreateDataTransferOptions()
                .MaximumTransferredFileNameCharacters);

        result.Should().HaveLength(128);
    }
}
