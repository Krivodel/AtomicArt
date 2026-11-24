using System.Globalization;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class NanoBanana2PanelTextFormatter
{
    private readonly ILocalizationTextProvider _textProvider;

    public NanoBanana2PanelTextFormatter(ILocalizationTextProvider textProvider)
    {
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
    }

    public string FormatAttachmentCounterText(
        int attachedImagesCount,
        int maxAttachedImages)
    {
        return _textProvider.Format(
            GenerationUiLocalizationKeys.Attachments.CounterFormat,
            attachedImagesCount,
            maxAttachedImages);
    }

    public string FormatGenerateButtonText(decimal price, string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return _textProvider.Get(GenerationUiLocalizationKeys.Actions.Generate);
        }

        return _textProvider.Format(
            GenerationUiLocalizationKeys.Actions.GenerateWithPrice,
            FormatPrice(price, currency));
    }

    public string FormatChatVariantsCount(int count)
    {
        int lastTwoDigits = count % 100;
        int lastDigit = count % 10;
        string key = lastTwoDigits is >= 11 and <= 14 ? GenerationChatLocalizationKeys.VariantsMany
            : lastDigit == 1 ? GenerationChatLocalizationKeys.VariantsOne
            : lastDigit is >= 2 and <= 4 ? GenerationChatLocalizationKeys.VariantsFew
            : GenerationChatLocalizationKeys.VariantsMany;

        return _textProvider.Format(key, count);
    }

    private static string FormatPrice(decimal price, string currency)
    {
        return string.Create(
            CultureInfo.CurrentCulture,
            $"{price:0.##} {currency}");
    }
}
