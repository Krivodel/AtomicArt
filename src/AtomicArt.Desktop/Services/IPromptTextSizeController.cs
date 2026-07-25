namespace AtomicArt.Desktop.Services;

public interface IPromptTextSizeController
{
    double CurrentTextSize { get; }

    event EventHandler? TextSizeChanged;

    Task AdjustAsync(PromptTextSizeAdjustment adjustment, CancellationToken ct);
}
