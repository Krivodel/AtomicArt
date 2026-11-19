using System.Text.Json.Serialization;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

public sealed record LocalCodexOptions(
    bool Enabled,
    string Folder = "",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ExecutablePath = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HomeDirectory = null)
{
    public static LocalCodexOptions Default { get; } = new(Enabled: true);

    internal string DisplayFolder => Folder.Length > 0 ? Folder
        : !string.IsNullOrEmpty(HomeDirectory) ? HomeDirectory
        : Path.GetDirectoryName(ExecutablePath) ?? string.Empty;
}
