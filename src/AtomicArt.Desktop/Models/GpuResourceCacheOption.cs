namespace AtomicArt.Desktop.Models;

public sealed record GpuResourceCacheOption(
    string DisplayName,
    string Value,
    int? Megabytes);
