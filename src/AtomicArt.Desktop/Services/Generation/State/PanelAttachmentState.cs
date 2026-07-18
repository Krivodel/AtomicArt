namespace AtomicArt.Desktop.Services.Generation.State;

public sealed class PanelAttachmentState
{
    public string Id { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string InternalFileName { get; init; } = string.Empty;
}
