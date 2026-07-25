namespace AtomicArt.Desktop.Services.Settings;

internal sealed class PromptTextSizeNumericSettingValueSource : INumericSettingValueSource
{
    public double CurrentValue => _promptTextSizeService.CurrentTextSize;

    public event EventHandler? ValueChanged
    {
        add => _promptTextSizeService.TextSizeChanged += value;
        remove => _promptTextSizeService.TextSizeChanged -= value;
    }

    private readonly IPromptTextSizeService _promptTextSizeService;

    public PromptTextSizeNumericSettingValueSource(
        IPromptTextSizeService promptTextSizeService)
    {
        _promptTextSizeService = promptTextSizeService
            ?? throw new ArgumentNullException(nameof(promptTextSizeService));
    }
}
