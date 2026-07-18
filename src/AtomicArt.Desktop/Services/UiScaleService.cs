namespace AtomicArt.Desktop.Services;

public sealed class UiScaleService : IUiScaleService
{
    public double CurrentScale { get; private set; } = UiScaleDefaults.DefaultScale;

    public event EventHandler? ScaleChanged;

    public void SetScale(double scale)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "UI scale must be positive.");
        }

        if (CurrentScale.Equals(scale))
        {
            return;
        }

        CurrentScale = scale;
        ScaleChanged?.Invoke(this, EventArgs.Empty);
    }
}
