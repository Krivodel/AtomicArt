using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services.Settings;

public static class NumericSettingOptionMatcher
{
    public static NumericSettingOption? FindByValue(
        IReadOnlyList<NumericSettingOption> options,
        double value)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (NumericSettingOption option in options)
        {
            if (option.Value.Equals(value))
            {
                return option;
            }
        }

        return null;
    }

    public static NumericSettingOption? FindByValueOrFirst(
        IReadOnlyList<NumericSettingOption> options,
        double value)
    {
        NumericSettingOption? matchedOption = FindByValue(options, value);

        if (matchedOption is not null)
        {
            return matchedOption;
        }

        return options.Count > 0
            ? options[0]
            : null;
    }

    public static bool ContainsValue(
        IReadOnlyList<NumericSettingOption> options,
        double value)
    {
        return FindByValue(options, value) is not null;
    }
}
