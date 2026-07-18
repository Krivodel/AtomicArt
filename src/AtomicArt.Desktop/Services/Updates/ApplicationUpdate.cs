using Velopack;

namespace AtomicArt.Desktop.Services.Updates;

public sealed class ApplicationUpdate
{
    public string Version { get; }

    internal UpdateInfo? NativeUpdate { get; }

    public ApplicationUpdate(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        Version = version;
    }

    internal ApplicationUpdate(UpdateInfo nativeUpdate)
    {
        ArgumentNullException.ThrowIfNull(nativeUpdate);

        Version = nativeUpdate.TargetFullRelease.Version.ToString();
        NativeUpdate = nativeUpdate;
    }
}
