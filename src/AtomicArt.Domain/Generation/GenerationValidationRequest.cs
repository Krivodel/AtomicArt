namespace AtomicArt.Domain.Generation;

public sealed record GenerationValidationRequest
{
    public GenerationModelConstraints Constraints { get; }
    public string? Prompt { get; }
    public string AspectRatio { get; }
    public string Resolution { get; }
    public double Temperature { get; }
    public int GenerationCount { get; }
    public IReadOnlyList<GenerationAttachedImage> AttachedImages { get; }
    public string? ThinkingLevel { get; }

    public GenerationValidationRequest(
        GenerationModelConstraints constraints,
        string? prompt,
        string aspectRatio,
        string resolution,
        double temperature,
        int generationCount,
        IReadOnlyList<GenerationAttachedImage> attachedImages,
        string? thinkingLevel = null)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(aspectRatio);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(attachedImages);

        Constraints = constraints;
        Prompt = prompt;
        AspectRatio = aspectRatio;
        Resolution = resolution;
        Temperature = temperature;
        GenerationCount = generationCount;
        AttachedImages = attachedImages;
        ThinkingLevel = thinkingLevel;
    }
}
