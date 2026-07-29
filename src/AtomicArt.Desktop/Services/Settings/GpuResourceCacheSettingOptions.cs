using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services.Settings;

public static class GpuResourceCacheSettingOptions
{
    public const int DefaultMegabytes = 128;
    public const string AutoValue = "auto";

    private const string MegabyteSuffix = "mb";

    public static IReadOnlyList<GpuResourceCacheOptionDefinition> Options { get; } =
    [
        new(CommonLocalizationKeys.Auto, AutoValue, null),
        new(SettingsLocalizationKeys.GpuCache.MegabytesFormat, FormatMegabytes(64), 64),
        new(SettingsLocalizationKeys.GpuCache.MegabytesFormat, FormatMegabytes(128), 128),
        new(SettingsLocalizationKeys.GpuCache.MegabytesFormat, FormatMegabytes(256), 256),
        new(SettingsLocalizationKeys.GpuCache.MegabytesFormat, FormatMegabytes(512), 512)
    ];

    public static GpuResourceCacheOptionDefinition DefaultOption => Options[0];

    public static int ResolveMegabytes(string? value)
    {
        GpuResourceCacheOptionDefinition? option = FindByValueOrDefaultOrNull(value);

        if (option is not null)
        {
            return option.Megabytes ?? DefaultMegabytes;
        }

        return DefaultMegabytes;
    }

    public static long ResolveBytes(string? value)
    {
        return ResolveMegabytes(value) * 1024L * 1024L;
    }

    public static GpuResourceCacheOptionDefinition FindByValueOrDefault(string? value)
    {
        GpuResourceCacheOptionDefinition? option = FindByValueOrDefaultOrNull(value);

        return option ?? DefaultOption;
    }

    private static GpuResourceCacheOptionDefinition? FindByValueOrDefaultOrNull(
        string? value)
    {
        foreach (GpuResourceCacheOptionDefinition option in Options)
        {
            if (string.Equals(option.Value, value, StringComparison.Ordinal))
            {
                return option;
            }
        }

        return null;
    }

    private static string FormatMegabytes(int megabytes)
    {
        return string.Concat(megabytes, MegabyteSuffix);
    }
}
