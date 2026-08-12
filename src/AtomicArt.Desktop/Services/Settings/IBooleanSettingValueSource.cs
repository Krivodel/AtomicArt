namespace AtomicArt.Desktop.Services.Settings;

public interface IBooleanSettingValueSource
{
    bool CurrentValue { get; }

    event EventHandler? ValueChanged;
}
