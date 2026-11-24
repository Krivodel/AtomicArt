namespace AtomicArt.Desktop.Services;

public interface ITextSizeController
{
    double CurrentTextSize { get; }

    event EventHandler? TextSizeChanged;

    Task AdjustAsync(TextSizeAdjustment adjustment, CancellationToken ct);
}
