using System.Globalization;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationPriceFormatter
{
    private const string UsdCurrencyCode = "USD";

    public string? Format(GenerationPriceDto? price)
    {
        if (price is null)
        {
            return null;
        }

        string formattedAmount = price.Amount.ToString("0.####", CultureInfo.InvariantCulture);

        if (string.Equals(price.CurrencyCode, UsdCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return $"${formattedAmount}";
        }

        return $"{price.CurrencyCode} {formattedAmount}";
    }
}
