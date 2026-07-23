using System.Globalization;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationPriceFormatter
{
    private const string UsdCurrencyCode = "USD";

    public string FormatAmount(GenerationPriceDto price)
    {
        ArgumentNullException.ThrowIfNull(price);

        return price.Amount.ToString("0.####", CultureInfo.InvariantCulture);
    }

    public string FormatCurrency(GenerationPriceDto price)
    {
        ArgumentNullException.ThrowIfNull(price);

        if (string.Equals(
            price.CurrencyCode,
            UsdCurrencyCode,
            StringComparison.OrdinalIgnoreCase))
        {
            return "$";
        }

        return price.CurrencyCode;
    }
}
