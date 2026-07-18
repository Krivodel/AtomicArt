using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services;

public sealed record GenerationStartSnapshot
{
    public string ModelId { get; }
    public string ModelDisplayName { get; }
    public string Prompt { get; }
    public string AspectRatio { get; }
    public string Resolution { get; }
    public int GenerationCount { get; }
    public int AttachedImagesCount { get; }
    public DateTime RequestedAtUtc { get; }

    public GenerationStartSnapshot(
        string modelId,
        string modelDisplayName,
        string prompt,
        string aspectRatio,
        string resolution,
        int generationCount,
        int attachedImagesCount,
        DateTime requestedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(modelDisplayName);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(aspectRatio);
        ArgumentNullException.ThrowIfNull(resolution);

        if (generationCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generationCount),
                generationCount,
                GenerationRequestValidationMessages.PositiveGenerationCountRequired);
        }

        if (attachedImagesCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attachedImagesCount),
                attachedImagesCount,
                "Attached images count must not be negative.");
        }

        ModelId = modelId;
        ModelDisplayName = modelDisplayName;
        Prompt = prompt;
        AspectRatio = aspectRatio;
        Resolution = resolution;
        GenerationCount = generationCount;
        AttachedImagesCount = attachedImagesCount;
        RequestedAtUtc = requestedAtUtc;
    }
}
