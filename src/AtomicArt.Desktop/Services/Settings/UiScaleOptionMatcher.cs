using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services.Settings;

public static class UiScaleOptionMatcher
{
    public static UiScaleOption? FindByValue(IReadOnlyList<UiScaleOption> options, double scale)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (UiScaleOption option in options)
        {
            if (option.Value.Equals(scale))
            {
                return option;
            }
        }

        return null;
    }

    public static UiScaleOption? FindByValueOrFirst(IReadOnlyList<UiScaleOption> options, double scale)
    {
        UiScaleOption? matchedOption = FindByValue(options, scale);

        if (matchedOption is not null)
        {
            return matchedOption;
        }

        return options.Count > 0
            ? options[0]
            : null;
    }

    public static bool ContainsValue(IReadOnlyList<UiScaleOption> options, double scale)
    {
        return FindByValue(options, scale) is not null;
    }
}
