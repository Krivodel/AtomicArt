using System.Globalization;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationPriceFormatterTests
{
    private readonly GenerationPriceFormatter _formatter = new();

    [Fact]
    public void FormatAmount_WithPrice_ReturnsCurrentCultureAmount()
    {
        GenerationPriceDto price = new(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage);

        string result = _formatter.FormatAmount(price);

        result.Should().Be(0.0678m.ToString("0.####", CultureInfo.CurrentCulture));
    }

    [Fact]
    public void FormatCurrency_WithUsdPrice_ReturnsDollarSymbol()
    {
        GenerationPriceDto price = new(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage);

        string result = _formatter.FormatCurrency(price);

        result.Should().Be("$");
    }
}
