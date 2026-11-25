using System.Text.Json;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

public sealed class LocalCodexSettings
{
    public LocalCodexOptions Current => _current;

    public event EventHandler? AvailabilityChanged;
    public event EventHandler? OptionsChanged;
    public event EventHandler? ConnectionChanged;

    private volatile LocalCodexOptions _current = LocalCodexOptions.Default;

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

        if (options is not { Folder: not null, SystemPrompt: not null } || options == _current)
        {
            return;
        }

        bool availabilityChanged = _current.Enabled != options.Enabled;
        bool connectionChanged = (options with { SystemPrompt = _current.SystemPrompt }) != _current;
        _current = options;
        OptionsChanged?.Invoke(this, EventArgs.Empty);

        if (connectionChanged)
        {
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }

        if (availabilityChanged)
        {
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
