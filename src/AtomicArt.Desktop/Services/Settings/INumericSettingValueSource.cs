namespace AtomicArt.Desktop.Services.Settings;

public interface INumericSettingValueSource
{
    double CurrentValue { get; }

    event EventHandler? ValueChanged;
}
