namespace AtomicArt.Desktop.Services.Settings;

internal sealed class TextSizeNumericSettingValueSource : INumericSettingValueSource
{
    public double CurrentValue => _textSizeService.CurrentTextSize;

    public event EventHandler? ValueChanged
    {
        add => _textSizeService.TextSizeChanged += value;
        remove => _textSizeService.TextSizeChanged -= value;
    }

    private readonly ITextSizeService _textSizeService;

    public TextSizeNumericSettingValueSource(
        ITextSizeService textSizeService)
    {
        _textSizeService = textSizeService
            ?? throw new ArgumentNullException(nameof(textSizeService));
    }
}
