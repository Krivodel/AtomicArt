namespace AtomicArt.Desktop.Services.Settings;

internal sealed class UiScaleNumericSettingValueSource : INumericSettingValueSource
{
    public double CurrentValue => _uiScaleService.CurrentScale;

    public event EventHandler? ValueChanged
    {
        add => _uiScaleService.ScaleChanged += value;
        remove => _uiScaleService.ScaleChanged -= value;
    }

    private readonly IUiScaleService _uiScaleService;

    public UiScaleNumericSettingValueSource(IUiScaleService uiScaleService)
    {
        _uiScaleService = uiScaleService ?? throw new ArgumentNullException(nameof(uiScaleService));
    }
}
