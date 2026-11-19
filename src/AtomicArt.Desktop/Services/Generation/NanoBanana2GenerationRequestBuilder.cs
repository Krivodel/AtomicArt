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
        string? thinkingLevel = null,
        string? quality = null,
        bool includeTemperature = true,
        string? complexity = null)
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

        return new ImageGenerationRequestDto(
            selectedModel.Id,
            prompt,
            aspectRatio,
            resolution,
            temperature,
            generationCount,
            attachedImages,
            validatedThinkingLevel,
            quality,
            includeTemperature,
            complexity);
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
            parameters.ThinkingLevel,
            parameters.Quality,
            parameters.IncludeTemperature,
            parameters.Complexity);
    }

    internal GenerationStartSnapshot CreateStartSnapshot(
        ImageGenerationRequestDto request,
        NanoBanana2GenerationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!string.Equals(request.ModelId, parameters.SelectedModel.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("Request and model metadata must refer to the same model.", nameof(parameters));
        }

        Dictionary<string, GenerationSavedParameter> savedParameters = GenerationWireParameters
            .Create(request)
            .ToDictionary(
                parameter => parameter.Key,
                parameter => GenerationParameterText.Describe(
                    parameter.Key,
                    parameter.Value,
                    parameters.SelectedModel),
                StringComparer.Ordinal);
        GenerationRequestSettings settings = new(savedParameters);

        return new GenerationStartSnapshot(
            request.ModelId,
            parameters.ModelDisplayName,
            request.Prompt,
            request.AspectRatio,
            request.Resolution,
            request.GenerationCount,
            request.AttachedImages.Count,
            DateTime.UtcNow,
            settings);
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
                GenerationRequestValidationMessages.PositiveGenerationCountRequired);
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
