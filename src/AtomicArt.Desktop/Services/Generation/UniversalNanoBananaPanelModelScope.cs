using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class UniversalNanoBananaPanelModelScope : IGenerationModelService
{
    public bool SupportsModel(ImageModelOption model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return IsSupportedProvider(model.Provider)
            && string.Equals(model.PanelId, GenerationPanelIds.NanoBanana, StringComparison.Ordinal);
    }

    private static bool IsSupportedProvider(string provider)
    {
        return string.Equals(provider, GenerationProviderIds.Google, StringComparison.Ordinal)
            || string.Equals(provider, GenerationProviderIds.Test, StringComparison.Ordinal);
    }
}
