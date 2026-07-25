using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingPromptTextSizeController : IPromptTextSizeController
{
    public double CurrentTextSize { get; private set; } = 14d;
    public PromptTextSizeAdjustment? LastAdjustment { get; private set; }

    public event EventHandler? TextSizeChanged;

    public Task AdjustAsync(PromptTextSizeAdjustment adjustment, CancellationToken ct)
    {
        LastAdjustment = adjustment;
        CurrentTextSize += adjustment == PromptTextSizeAdjustment.Increase ? 1d : -1d;
        TextSizeChanged?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }
}
