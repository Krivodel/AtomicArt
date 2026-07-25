namespace AtomicArt.Desktop.Services;

public interface IPromptTextSizeService
{
    double CurrentTextSize { get; }

    event EventHandler? TextSizeChanged;

    void SetTextSize(double textSize);
}
