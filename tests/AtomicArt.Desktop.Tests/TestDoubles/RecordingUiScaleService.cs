using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingUiScaleService : IUiScaleService
{
    public double CurrentScale { get; private set; }

    public event EventHandler? ScaleChanged;

    public RecordingUiScaleService()
        : this(UiScaleDefaults.DefaultScale)
    {
    }

    public RecordingUiScaleService(double initialScale)
    {
        CurrentScale = initialScale;
    }

    public void SetScale(double scale)
    {
        CurrentScale = scale;
        ScaleChanged?.Invoke(this, EventArgs.Empty);
    }
}
