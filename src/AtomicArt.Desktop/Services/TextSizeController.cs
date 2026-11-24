using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Services;

public abstract class TextSizeController : ITextSizeController
{
    public double CurrentTextSize => _textSizeService.CurrentTextSize;

    public event EventHandler? TextSizeChanged
    {
        add => _textSizeService.TextSizeChanged += value;
        remove => _textSizeService.TextSizeChanged -= value;
    }

    private readonly ITextSizeSettingDefinition _definition;
    private readonly ITextSizeService _textSizeService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IDoubleSettingValueConverter _valueConverter;

    protected TextSizeController(
        ITextSizeSettingDefinition definition,
        ITextSizeService textSizeService,
        ISettingsStateService settingsStateService,
        IDoubleSettingValueConverter valueConverter)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _textSizeService = textSizeService
            ?? throw new ArgumentNullException(nameof(textSizeService));
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
    }

    public async Task AdjustAsync(TextSizeAdjustment adjustment, CancellationToken ct)
    {
        int currentIndex = FindCurrentOptionIndex();
        int indexOffset = adjustment switch
        {
            TextSizeAdjustment.Decrease => -1,
            TextSizeAdjustment.Increase => 1,
            _ => throw new ArgumentOutOfRangeException(
                nameof(adjustment),
                adjustment,
                "Unsupported text size adjustment.")
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
            if (_definition.Options[index].Value.Equals(_textSizeService.CurrentTextSize))
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            "Current text size does not match a registered option.");
    }
}
