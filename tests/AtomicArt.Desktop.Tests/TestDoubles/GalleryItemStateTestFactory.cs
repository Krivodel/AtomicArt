using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal static class GalleryItemStateTestFactory
{
    private const string DefaultImagePath = "image.png";
    private const string DefaultPrompt = "Saved prompt";

    private static readonly DateTime CreatedAtUtc = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    public static GalleryItemState CreateGenerated(
        string prompt = DefaultPrompt,
        int secondsBefore = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        return new GalleryItemState
        {
            Id = Guid.NewGuid(),
            ModelId = ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ModelDisplayName = ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            Prompt = prompt,
            AspectRatio = GenerationAspectRatios.Auto,
            Resolution = TestGenerationOutputMetadata.GeneratedImageResolution,
            CreatedAtUtc = CreatedAtUtc.AddSeconds(-secondsBefore),
            Status = GenerationItemStatus.Generated,
            ImagePath = DefaultImagePath
        };
    }
}
