namespace AtomicArt.Desktop.Services.State;

public sealed record ImageViewerWindowLimitState
{
    public required bool IsEnabled { get; init; }
    public required int Count { get; init; }
}
