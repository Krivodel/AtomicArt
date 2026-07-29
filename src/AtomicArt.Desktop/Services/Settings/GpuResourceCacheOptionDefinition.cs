namespace AtomicArt.Desktop.Services.Settings;

public sealed record GpuResourceCacheOptionDefinition
{
    public string DisplayNameKey { get; }
    public string Value { get; }
    public int? Megabytes { get; }

    public GpuResourceCacheOptionDefinition(
        string displayNameKey,
        string value,
        int? megabytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayNameKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        DisplayNameKey = displayNameKey;
        Value = value;
        Megabytes = megabytes;
    }
}
