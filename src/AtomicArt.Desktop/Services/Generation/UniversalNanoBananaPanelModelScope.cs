using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class UniversalNanoBananaPanelModelScope : IGenerationModelService, IDisposable
{
    private readonly GenerationModelVisibilityService _visibilityService;

    public UniversalNanoBananaPanelModelScope(
        GenerationModelVisibilityService? visibilityService = null)
    {
        _visibilityService = visibilityService ?? new GenerationModelVisibilityService();
        _visibilityService.VisibleModelsChanged += OnVisibleModelsChanged;
    }

    public event EventHandler? VisibleModelsChanged;

    public void Dispose()
    {
        _visibilityService.VisibleModelsChanged -= OnVisibleModelsChanged;
    }

    public bool SupportsModel(ImageModelOption model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return _visibilityService.IsVisible(model.Id)
            && IsSupportedProvider(model.Provider)
            && string.Equals(model.PanelId, GenerationPanelIds.NanoBanana, StringComparison.Ordinal);
    }

    private static bool IsSupportedProvider(string provider)
    {
        return string.Equals(provider, GenerationProviderIds.Google, StringComparison.Ordinal)
            || string.Equals(provider, GenerationProviderIds.OpenRouter, StringComparison.Ordinal)
            || string.Equals(provider, GenerationProviderIds.Test, StringComparison.Ordinal);
    }

    private void OnVisibleModelsChanged(object? sender, EventArgs e)
    {
        VisibleModelsChanged?.Invoke(this, EventArgs.Empty);
    }
}
