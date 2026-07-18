namespace AtomicArt.Desktop.Services;

public interface IUiScaleService
{
    double CurrentScale { get; }

    event EventHandler? ScaleChanged;

    void SetScale(double scale);
}
