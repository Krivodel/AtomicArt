using System.Globalization;
using System.Text.Json;

using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class ImageViewerWindowLimitService
{
    public bool IsEnabled => _state.IsEnabled;
    public int Count => _state.Count;
    public int Limit => IsEnabled ? Count : ImageViewerWindowLimitSettingDefinition.Unlimited;

    public event EventHandler? ValueChanged;

    private ImageViewerWindowLimitState _state;

    public ImageViewerWindowLimitService(ISettingsDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        ImageViewerWindowLimitSettingDefinition definition = definitions.GetRequired<ImageViewerWindowLimitSettingDefinition>();
        _state = new ImageViewerWindowLimitState { IsEnabled = true, Count = definition.DefaultValue };
    }

    public void SetLimit(int limit)
    {
        SetLimit(
            limit != ImageViewerWindowLimitSettingDefinition.Unlimited,
            limit == ImageViewerWindowLimitSettingDefinition.Unlimited ? Count : limit);
    }

    public void SetLimit(bool isEnabled, int count)
    {
        if (!ImageViewerWindowLimitSettingDefinition.IsValidLimit(count))
        {
            throw new ArgumentOutOfRangeException(nameof(count), count,
                $"The open image limit must be a whole number between {ImageViewerWindowLimitSettingDefinition.MinimumLimit} and {ImageViewerWindowLimitSettingDefinition.MaximumLimit}.");
        }

        ImageViewerWindowLimitState nextState = new() { IsEnabled = isEnabled, Count = count };

        if (_state == nextState)
        {
            return;
        }

        _state = nextState;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetSerializedValue()
    {
        return JsonSerializer.Serialize(_state);
    }

    public void ApplySerializedValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int legacyLimit)
            && (legacyLimit == ImageViewerWindowLimitSettingDefinition.Unlimited
                || ImageViewerWindowLimitSettingDefinition.IsValidLimit(legacyLimit)))
        {
            SetLimit(legacyLimit);

            return;
        }

        ImageViewerWindowLimitState? state;

        try
        {
            state = JsonSerializer.Deserialize<ImageViewerWindowLimitState>(value);
        }
        catch (JsonException)
        {
            return;
        }

        if (state is not null && ImageViewerWindowLimitSettingDefinition.IsValidLimit(state.Count))
        {
            SetLimit(state.IsEnabled, state.Count);
        }
    }
}
