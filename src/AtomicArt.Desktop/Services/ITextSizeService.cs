namespace AtomicArt.Desktop.Services;

public interface ITextSizeService
{
    double CurrentTextSize { get; }

    event EventHandler? TextSizeChanged;

    void SetTextSize(double textSize);
}
