using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class ImageModelOptionCatalog : IImageModelOptionCatalog
{
    public bool IsLoaded
    {
        get
        {
            lock (_syncRoot)
            {
                return _isLoaded;
            }
        }
    }

    private readonly object _syncRoot = new();
    private IReadOnlyList<ImageModelOption> _models = [];
    private bool _isLoaded;

    public void Clear()
    {
        lock (_syncRoot)
        {
            _models = [];
            _isLoaded = false;
        }
    }

    public void Initialize(GenerationModelCatalogDto catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        IReadOnlyList<ImageModelOption> models = CreateSnapshot(catalog);

        lock (_syncRoot)
        {
            _models = models;
            _isLoaded = true;
        }
    }

    public IReadOnlyList<ImageModelOption> GetModels()
    {
        lock (_syncRoot)
        {
            return _models;
        }
    }

    private static IReadOnlyList<ImageModelOption> CreateSnapshot(GenerationModelCatalogDto catalog)
    {
        IReadOnlyList<GenerationModelMetadataDto>? modelMetadata = catalog.Models;

        if (modelMetadata is null || modelMetadata.Count == 0)
        {
            throw new InvalidOperationException("Generation model catalog must contain at least one model.");
        }

        return modelMetadata
            .Select(CreateOption)
            .OrderBy(option => option.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    private static ImageModelOption CreateOption(GenerationModelMetadataDto model)
    {
        ArgumentNullException.ThrowIfNull(model);

        string modelId = CreateRequiredSafeText(model.Id, nameof(model.Id));
        string displayName = CreateRequiredSafeText(model.DisplayName, nameof(model.DisplayName));
        string provider = CreateRequiredSafeText(model.Provider, nameof(model.Provider));
        string providerModelId = CreateRequiredSafeText(model.ProviderModelId, nameof(model.ProviderModelId));
        string panelId = CreateRequiredSafeText(model.PanelId, nameof(model.PanelId));

        if (model.Attachments is null)
        {
            throw new InvalidOperationException(
                $"Generation model '{modelId}' must contain attachment metadata.");
        }

        if (model.Pricing is null)
        {
            throw new InvalidOperationException(
                $"Generation model '{modelId}' must contain pricing metadata.");
        }

        return new ImageModelOption(
            modelId,
            displayName,
            provider,
            providerModelId,
            panelId,
            model.ContextWindowTokens,
            model.MaxOutputTokens,
            CreateStringSnapshot(model.AspectRatios),
            CreateStringSnapshot(model.Resolutions),
            CreateIntSnapshot(model.GenerationCounts),
            CreateTemperatureSnapshot(modelId, model.Temperature),
            model.Attachments.MaxCount,
            checked((int)model.Attachments.MaxSingleFileBytes),
            model.Attachments.MaxTotalBytes,
            CreateStringSnapshot(model.Attachments.SupportedContentTypes),
            model.Pricing,
            CreateThinkingSnapshot(modelId, model.Thinking));
    }

    private static string CreateRequiredSafeText(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Generation model metadata must contain a non-empty '{propertyName}' value.");
        }

        string trimmedValue = value.Trim();

        if (trimmedValue.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"Generation model metadata contains invalid control characters in '{propertyName}'.");
        }

        return trimmedValue;
    }

    private static IReadOnlyList<string> CreateStringSnapshot(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(value => value?.Trim() ?? string.Empty)
            .ToList();
    }

    private static IReadOnlyList<int> CreateIntSnapshot(IReadOnlyList<int>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values.ToList();
    }

    private static GenerationModelTemperatureMetadataDto CreateTemperatureSnapshot(
        string modelId,
        GenerationModelTemperatureMetadataDto? temperature)
    {
        if (temperature is null
            || !double.IsFinite(temperature.Minimum)
            || !double.IsFinite(temperature.Maximum)
            || !double.IsFinite(temperature.Default)
            || !double.IsFinite(temperature.Step)
            || temperature.Minimum < 0d
            || temperature.Maximum <= temperature.Minimum
            || temperature.Default < temperature.Minimum
            || temperature.Default > temperature.Maximum
            || temperature.Step <= 0d
            || temperature.Step > temperature.Maximum - temperature.Minimum
            || !GenerationTemperaturePolicy.IsSupported(temperature.Maximum, temperature)
            || !GenerationTemperaturePolicy.IsSupported(temperature.Default, temperature))
        {
            throw new InvalidOperationException(
                $"Generation model '{modelId}' must contain valid temperature metadata.");
        }

        return new GenerationModelTemperatureMetadataDto(
            temperature.Minimum,
            temperature.Maximum,
            temperature.Default,
            temperature.Step);
    }

    private static GenerationModelThinkingMetadataDto? CreateThinkingSnapshot(
        string modelId,
        GenerationModelThinkingMetadataDto? thinking)
    {
        if (thinking is null)
        {
            return null;
        }

        if (thinking.Levels is null || thinking.Levels.Count == 0)
        {
            throw new InvalidOperationException(
                $"Generation model '{modelId}' contains empty thinking metadata.");
        }

        List<GenerationModelThinkingLevelMetadataDto> levels = [];
        HashSet<string> uniqueValues = new(StringComparer.Ordinal);

        foreach (GenerationModelThinkingLevelMetadataDto? level in thinking.Levels)
        {
            if (level is null)
            {
                throw new InvalidOperationException(
                    $"Generation model '{modelId}' contains an empty thinking level.");
            }

            string value = CreateRequiredSafeText(level.Value, nameof(level.Value));
            string displayName = CreateRequiredSafeText(level.DisplayName, nameof(level.DisplayName));

            if (!uniqueValues.Add(value))
            {
                throw new InvalidOperationException(
                    $"Generation model '{modelId}' contains duplicated thinking level '{value}'.");
            }

            levels.Add(new GenerationModelThinkingLevelMetadataDto(value, displayName));
        }

        string defaultValue = CreateRequiredSafeText(thinking.Default, nameof(thinking.Default));

        if (!uniqueValues.Contains(defaultValue))
        {
            throw new InvalidOperationException(
                $"Generation model '{modelId}' contains unsupported default thinking level '{defaultValue}'.");
        }

        return new GenerationModelThinkingMetadataDto(levels.AsReadOnly(), defaultValue);
    }
}
