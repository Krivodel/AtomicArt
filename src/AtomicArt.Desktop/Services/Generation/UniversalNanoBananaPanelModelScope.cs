using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class UniversalNanoBananaPanelModelScope : IGenerationModelService, IDisposable
{
    private readonly GenerationModelVisibilityService _visibilityService;
    private readonly LocalCodexSettings? _localCodex;

    public UniversalNanoBananaPanelModelScope(
        GenerationModelVisibilityService? visibilityService = null,
        LocalCodexSettings? localCodex = null)
    {
        _visibilityService = visibilityService ?? new GenerationModelVisibilityService();
        _visibilityService.VisibleModelsChanged += OnVisibleModelsChanged;
        _localCodex = localCodex;
        if (_localCodex is not null)
        {
            _localCodex.AvailabilityChanged += OnLocalCodexAvailabilityChanged;
        }
    }

    public event EventHandler? VisibleModelsChanged;
    public event EventHandler? LocalCodexAvailabilityChanged;

    public void Dispose()
    {
        _visibilityService.VisibleModelsChanged -= OnVisibleModelsChanged;
        if (_localCodex is not null)
        {
            _localCodex.AvailabilityChanged -= OnLocalCodexAvailabilityChanged;
        }
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
        return LocalCodexImageModel.Matches(provider)
            || string.Equals(provider, GenerationProviderIds.Google, StringComparison.Ordinal)
            || string.Equals(provider, GenerationProviderIds.OpenRouter, StringComparison.Ordinal)
            || string.Equals(provider, GenerationProviderIds.Test, StringComparison.Ordinal);
    }

    private void OnVisibleModelsChanged(object? sender, EventArgs e)
    {
        VisibleModelsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLocalCodexAvailabilityChanged(object? sender, EventArgs e)
    {
        LocalCodexAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
