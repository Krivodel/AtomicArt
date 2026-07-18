using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services.Settings;

public static class GpuResourceCacheSettingOptions
{
    public const int DefaultMegabytes = 128;
    public const string AutoValue = "auto";

    private const string MegabyteSuffix = "mb";

    public static IReadOnlyList<GpuResourceCacheOption> Options { get; } =
    [
        new("Авто", AutoValue, null),
        new("64мб", FormatMegabytes(64), 64),
        new("128мб", FormatMegabytes(128), 128),
        new("256мб", FormatMegabytes(256), 256),
        new("512мб", FormatMegabytes(512), 512)
    ];

    public static GpuResourceCacheOption DefaultOption => Options[0];

    public static int ResolveMegabytes(string? value)
    {
        GpuResourceCacheOption? option = FindByValueOrDefaultOrNull(value);

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

    public static GpuResourceCacheOption FindByValueOrDefault(string? value)
    {
        GpuResourceCacheOption? option = FindByValueOrDefaultOrNull(value);

        return option ?? DefaultOption;
    }

    private static GpuResourceCacheOption? FindByValueOrDefaultOrNull(string? value)
    {
        foreach (GpuResourceCacheOption option in Options)
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
