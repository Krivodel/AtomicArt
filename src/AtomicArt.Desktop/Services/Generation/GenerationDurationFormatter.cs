using System.Globalization;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationDurationFormatter
{
    private readonly ILocalizationTextProvider _textProvider;

    public GenerationDurationFormatter(ILocalizationTextProvider textProvider)
    {
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
    }

    public string? Format(TimeSpan? duration)
    {
        if (duration is null)
        {
            return null;
        }

        int totalSeconds = Math.Max(0, (int)Math.Floor(duration.Value.TotalSeconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return string.Concat(
                FormatValue(hours, CommonLocalizationKeys.TimeUnits.HourShort),
                ":",
                FormatValue(minutes, CommonLocalizationKeys.TimeUnits.MinuteShort),
                ":",
                FormatValue(seconds, CommonLocalizationKeys.TimeUnits.SecondShort));
        }

        if (minutes > 0)
        {
            return string.Concat(
                FormatValue(minutes, CommonLocalizationKeys.TimeUnits.MinuteShort),
                ":",
                FormatValue(seconds, CommonLocalizationKeys.TimeUnits.SecondShort));
        }

        return FormatValue(seconds, CommonLocalizationKeys.TimeUnits.SecondShort);
    }

    private string FormatValue(int value, string unitKey)
    {
        return string.Concat(
            value.ToString(CultureInfo.CurrentCulture),
            _textProvider.Get(unitKey));
    }
}
