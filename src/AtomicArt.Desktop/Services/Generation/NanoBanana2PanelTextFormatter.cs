using System.Globalization;

using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services.Generation;

internal static class NanoBanana2PanelTextFormatter
{
    public static string FormatAttachmentCounterText(int attachedImagesCount, int maxAttachedImages)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            UiStrings.AttachmentCounterFormat,
            attachedImagesCount,
            maxAttachedImages);
    }

    public static string FormatGenerateButtonText(decimal price, string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return UiStrings.GenerateButtonText;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            UiStrings.GenerateButtonFormat,
            FormatPrice(price, currency));
    }

    public static string FormatTemperatureText(double temperature)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            UiStrings.TemperatureValueFormat,
            temperature);
    }

    private static string FormatPrice(decimal price, string currency)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{price:0.##} {currency}");
    }
}
