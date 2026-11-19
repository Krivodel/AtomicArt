namespace AtomicArt.Desktop.Services.Settings;

public sealed class SaveReferencesSetting : IBooleanSettingValueSource, ISettingsStateApplicator
{
    public string SettingKey => SaveReferencesSettingDefinition.KeyValue;
    public bool CurrentValue => _currentValue;

    public event EventHandler? ValueChanged;

    private readonly IBooleanSettingValueConverter _valueConverter;
    private volatile bool _currentValue = SaveReferencesSettingDefinition.DefaultValue;

    public SaveReferencesSetting(IBooleanSettingValueConverter valueConverter)
    {
        _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
    }

    public void Apply(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (_valueConverter.TryParse(value, out bool saveReferences) && saveReferences != _currentValue)
        {
            _currentValue = saveReferences;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
