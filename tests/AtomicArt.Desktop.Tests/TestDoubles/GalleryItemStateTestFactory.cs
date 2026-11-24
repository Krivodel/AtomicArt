using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal static class GalleryItemStateTestFactory
{
    private const string DefaultImagePath = "image.png";
    private const string DefaultPrompt = "Saved prompt";

    private static readonly DateTime CreatedAtUtc = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    public static GalleryItemState CreateGenerated(
        string prompt = DefaultPrompt,
        int secondsBefore = 0,
        Guid? id = null,
        DateTime? createdAtUtc = null,
        DateTime? galleryOrderTimestampUtc = null,
        string? imagePath = DefaultImagePath,
        string? thumbnailPath = null,
        DateTime? completedAtUtc = null,
        TimeSpan? generationDuration = null,
        GenerationPriceDto? price = null,
        GenerationUsageDto? usage = null,
        int attachedImagesCount = 0,
        bool isFavorite = false,
        GenerationRequestSettings? requestSettings = null,
        IReadOnlyList<GenerationReference>? references = null,
        GenerationChatHistory? chatHistory = null,
        GenerationChatResult? chatResult = null,
        string? modelId = null,
        Guid? chatId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        return new GalleryItemState
        {
            Id = id ?? Guid.NewGuid(),
            ModelId = modelId ?? ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ModelDisplayName = ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            Prompt = prompt,
            AspectRatio = GenerationAspectRatios.Auto,
            Resolution = TestGenerationOutputMetadata.GeneratedImageResolution,
            RequestSettings = requestSettings,
            CreatedAtUtc = createdAtUtc ?? CreatedAtUtc.AddSeconds(-secondsBefore),
            GalleryOrderTimestampUtc = galleryOrderTimestampUtc,
            Status = GenerationItemStatus.Generated,
            ImagePath = imagePath,
            ThumbnailPath = thumbnailPath,
            CompletedAtUtc = completedAtUtc,
            GenerationDuration = generationDuration,
            Price = price,
            Usage = usage,
            AttachedImagesCount = attachedImagesCount,
            References = references,
            ChatId = chatId,
            ChatHistory = chatHistory,
            ChatResult = chatResult,
            IsFavorite = isFavorite
        };
    }
}
