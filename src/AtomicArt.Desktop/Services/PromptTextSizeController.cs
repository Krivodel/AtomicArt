using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Services;

public sealed class PromptTextSizeController : IPromptTextSizeController
{
    public double CurrentTextSize => _promptTextSizeService.CurrentTextSize;

    public event EventHandler? TextSizeChanged
    {
        add => _promptTextSizeService.TextSizeChanged += value;
        remove => _promptTextSizeService.TextSizeChanged -= value;
    }

    private readonly IPromptTextSizeSettingDefinition _definition;
    private readonly IPromptTextSizeService _promptTextSizeService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IDoubleSettingValueConverter _valueConverter;

    public PromptTextSizeController(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        IPromptTextSizeService promptTextSizeService,
        ISettingsStateService settingsStateService,
        IDoubleSettingValueConverter valueConverter)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);

        _definition = settingsDefinitionCatalog.GetRequired<PromptTextSizeSettingDefinition>();
        _promptTextSizeService = promptTextSizeService
            ?? throw new ArgumentNullException(nameof(promptTextSizeService));
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
    }

    public async Task AdjustAsync(PromptTextSizeAdjustment adjustment, CancellationToken ct)
    {
        int currentIndex = FindCurrentOptionIndex();
        int indexOffset = adjustment switch
        {
            PromptTextSizeAdjustment.Decrease => -1,
            PromptTextSizeAdjustment.Increase => 1,
            _ => throw new ArgumentOutOfRangeException(
                nameof(adjustment),
                adjustment,
                "Unsupported prompt text size adjustment.")
        };
        int targetIndex = Math.Clamp(
            currentIndex + indexOffset,
            0,
            _definition.Options.Count - 1);

        if (targetIndex == currentIndex)
        {
            return;
        }

        NumericSettingOption targetOption = _definition.Options[targetIndex];
        string value = _valueConverter.Format(targetOption.Value);
        _settingsStateService.ApplyValue(_definition, value);
        await _settingsStateService.SaveValueAsync(_definition, value, ct).ConfigureAwait(false);
    }

    private int FindCurrentOptionIndex()
    {
        for (int index = 0; index < _definition.Options.Count; index++)
        {
            if (_definition.Options[index].Value.Equals(_promptTextSizeService.CurrentTextSize))
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            "Current prompt text size does not match a registered option.");
    }
}
