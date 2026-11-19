using System.Text.Json;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

public sealed class LocalCodexSettings
{
    private volatile LocalCodexOptions _current = LocalCodexOptions.Default;

    public event EventHandler? AvailabilityChanged;
    public event EventHandler? OptionsChanged;

    public LocalCodexOptions Current => _current;

    internal void ApplySerializedValue(string value)
    {
        LocalCodexOptions? options;

        try
        {
            options = JsonSerializer.Deserialize<LocalCodexOptions>(value);
        }
        catch (JsonException)
        {
            return;
        }

        if (options is not { Folder: not null } || options == _current)
        {
            return;
        }

        bool availabilityChanged = _current.Enabled != options.Enabled;
        _current = options;
        OptionsChanged?.Invoke(this, EventArgs.Empty);

        if (availabilityChanged)
        {
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
