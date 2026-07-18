namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationPanelOptionDefaults
{
    public static ImageModelOption GetDefaultModel(IReadOnlyList<ImageModelOption> availableModels)
    {
        return availableModels.FirstOrDefault()
            ?? throw new InvalidOperationException("No image generation models are registered.");
    }

    public static string GetDefaultAspectRatio(ImageModelOption selectedModel)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);

        return selectedModel.AspectRatios.FirstOrDefault()
            ?? throw new InvalidOperationException("No aspect ratios are registered for the selected model.");
    }

    public static string GetDefaultResolution(ImageModelOption selectedModel)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);

        return selectedModel.Resolutions.FirstOrDefault()
            ?? throw new InvalidOperationException("No resolutions are registered for the selected model.");
    }

    public static int GetDefaultGenerationCount(ImageModelOption selectedModel)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);

        return selectedModel.GenerationCounts.FirstOrDefault();
    }

    public static double GetDefaultTemperature(ImageModelOption selectedModel)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);

        return selectedModel.Temperature.Default;
    }

}
