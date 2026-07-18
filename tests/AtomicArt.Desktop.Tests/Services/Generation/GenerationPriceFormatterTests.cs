using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationPriceFormatterTests
{
    private readonly GenerationPriceFormatter _formatter = new();

    [Fact]
    public void Format_WithUsdAmount_ReturnsDollarText()
    {
        GenerationPriceDto price = new(0.0678m, "USD", "ActualProviderUsage");

        string? result = _formatter.Format(price);

        result.Should().Be("$0.0678");
    }

    [Fact]
    public void Format_WithMissingPrice_ReturnsNull()
    {
        string? result = _formatter.Format(null);

        result.Should().BeNull();
    }
}
