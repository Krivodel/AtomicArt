using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Generation;

internal static class NanoBanana2PanelDefaults
{
    public static ImageModelOption GetDefaultModel(IReadOnlyList<ImageModelOption> availableModels)
    {
        return GenerationPanelOptionDefaults.GetDefaultModel(availableModels);
    }

    public static string GetDefaultAspectRatio(ImageModelOption selectedModel)
    {
        return GenerationPanelOptionDefaults.GetDefaultAspectRatio(selectedModel);
    }

    public static string GetDefaultResolution(ImageModelOption selectedModel)
    {
        return GenerationPanelOptionDefaults.GetDefaultResolution(selectedModel);
    }

    public static int GetDefaultGenerationCount(ImageModelOption selectedModel)
    {
        return GenerationPanelOptionDefaults.GetDefaultGenerationCount(selectedModel);
    }

    public static double GetDefaultTemperature(ImageModelOption selectedModel)
    {
        return GenerationPanelOptionDefaults.GetDefaultTemperature(selectedModel);
    }
}
