namespace AtomicArt.Desktop.Services.Settings;

public interface ISettingsStateService
{
    Task ApplySavedSettingsAsync(CancellationToken ct);

    void ApplyValue(ISettingsDefinition definition, string value);

    Task<string?> LoadValueAsync(ISettingsDefinition definition, CancellationToken ct);

    Task SaveValueAsync(ISettingsDefinition definition, string value, CancellationToken ct);
}
