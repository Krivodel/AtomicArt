using System.Text.Json;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationModelVisibilityService
{
    private readonly HashSet<string> _visibleModelIds = new(
        GenerationModelVisibilitySettingDefinition.SupportedModelIds,
        StringComparer.Ordinal);

    public event EventHandler? VisibleModelsChanged;

    public bool IsVisible(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        return !GenerationModelVisibilitySettingDefinition.SupportedModelIds.Contains(modelId)
            || _visibleModelIds.Contains(modelId);
    }

    public IReadOnlyList<string> GetVisibleModelIds()
    {
        return _visibleModelIds.Order(StringComparer.Ordinal).ToList();
    }

    public string GetSerializedValue()
    {
        return JsonSerializer.Serialize(GetVisibleModelIds());
    }

    public void ApplySerializedValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        IReadOnlyList<string>? ids;

        try
        {
            ids = JsonSerializer.Deserialize<IReadOnlyList<string>>(value);
        }
        catch (JsonException)
        {
            return;
        }

        if (ids is not null)
        {
            SetVisibleModelIds(ids);
        }
    }

    public void SetVisibleModelIds(IEnumerable<string> modelIds)
    {
        ArgumentNullException.ThrowIfNull(modelIds);

        HashSet<string> nextIds = modelIds
            .Where(GenerationModelVisibilitySettingDefinition.SupportedModelIds.Contains)
            .ToHashSet(StringComparer.Ordinal);

        if (_visibleModelIds.SetEquals(nextIds))
        {
            return;
        }

        _visibleModelIds.Clear();
        _visibleModelIds.UnionWith(nextIds);
        VisibleModelsChanged?.Invoke(this, EventArgs.Empty);
    }
}
