namespace AtomicArt.Desktop.Services.Generation.State;

public sealed class GenerationPanelState
{
    public string PanelId { get; init; } = string.Empty;
    public string SelectedModelId { get; init; } = string.Empty;
    public string AspectRatio { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    public double? Temperature { get; init; }
    public string? ThinkingLevel { get; init; }
    public int GenerationCount { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public IReadOnlyList<PanelAttachmentState> Attachments { get; init; } =
        [];
}
