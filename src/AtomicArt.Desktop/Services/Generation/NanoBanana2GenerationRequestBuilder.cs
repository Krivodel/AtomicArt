using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class NanoBanana2GenerationRequestBuilder : IGenerationModelService
{
    public ImageGenerationRequestDto CreateValidatedRequest(
        ImageModelOption selectedModel,
        string prompt,
        string aspectRatio,
        string resolution,
        double temperature,
        int generationCount,
        IReadOnlyList<AttachedImageDto> attachedImages,
        string? thinkingLevel = null)
    {
        ValidateRequestParameters(
            selectedModel,
            prompt,
            aspectRatio,
            resolution,
            temperature,
            generationCount,
            attachedImages);

        string? validatedThinkingLevel = ResolveThinkingLevel(thinkingLevel, selectedModel);

        return CreateRequest(
            selectedModel,
            prompt,
            aspectRatio,
            resolution,
            temperature,
            generationCount,
            attachedImages,
            validatedThinkingLevel);
    }

    internal ImageGenerationRequestDto CreateValidatedRequest(NanoBanana2GenerationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return CreateValidatedRequest(
            parameters.SelectedModel,
            parameters.Prompt,
            parameters.AspectRatio,
            parameters.Resolution,
            parameters.Temperature,
            parameters.GenerationCount,
            parameters.AttachedImages,
            parameters.ThinkingLevel);
    }

    public GenerationStartSnapshot CreateStartSnapshot(
        ImageGenerationRequestDto request,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new GenerationStartSnapshot(
            request.ModelId,
            displayName,
            request.Prompt,
            request.AspectRatio,
            request.Resolution,
            request.GenerationCount,
            request.AttachedImages.Count,
            DateTime.UtcNow);
    }

    private static ImageGenerationRequestDto CreateRequest(
        ImageModelOption selectedModel,
        string prompt,
        string aspectRatio,
        string resolution,
        double temperature,
        int generationCount,
        IReadOnlyList<AttachedImageDto> attachedImages,
        string? thinkingLevel)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(aspectRatio);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(attachedImages);

        return new ImageGenerationRequestDto(
            selectedModel.Id,
            prompt,
            aspectRatio,
            resolution,
            temperature,
            generationCount,
            attachedImages,
            thinkingLevel);
    }

    private static void ValidateRequestParameters(
        ImageModelOption selectedModel,
        string prompt,
        string aspectRatio,
        string resolution,
        double temperature,
        int generationCount,
        IReadOnlyList<AttachedImageDto> attachedImages)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(aspectRatio);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolution);
        ArgumentNullException.ThrowIfNull(attachedImages);

        bool temperatureWasReset = GenerationPanelOptionCompatibility.ResolveTemperature(
                temperature,
                selectedModel.Temperature)
            .WasReset;

        if (temperatureWasReset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperature),
                "Temperature must match the selected model metadata.");
        }

        if (generationCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generationCount),
                "Generation count must be positive.");
        }
    }

    private static string? ResolveThinkingLevel(
        string? thinkingLevel,
        ImageModelOption selectedModel)
    {
        (string? value, bool wasReset) = GenerationPanelOptionCompatibility.ResolveThinkingLevel(
            thinkingLevel,
            selectedModel.Thinking);

        if (wasReset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(thinkingLevel),
                "Thinking level must match the selected model metadata.");
        }

        return value;
    }
}
