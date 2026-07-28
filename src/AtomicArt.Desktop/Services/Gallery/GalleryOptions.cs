namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryOptions
{
    public const string SectionName = "Gallery";

    public int ElapsedRefreshIntervalMilliseconds { get; init; }
    public int MaximumPooledCardControlCount { get; init; }
    public long MaximumPreviewCacheSizeBytes { get; init; }
    public int MaximumPreviewDecodeConcurrency { get; init; }
    public int MaximumPreviewPresentationsPerFrame { get; init; }
    public int MaximumThumbnailCreationConcurrency { get; init; }
    public long MaximumThumbnailSourceImageBytes { get; init; }
    public int OrderTimestampIntervalMilliseconds { get; init; }

    public static bool IsValid(GalleryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.ElapsedRefreshIntervalMilliseconds > 0
            && options.MaximumPooledCardControlCount >= 0
            && options.MaximumPreviewCacheSizeBytes > 0L
            && options.MaximumPreviewDecodeConcurrency > 0
            && options.MaximumPreviewPresentationsPerFrame > 0
            && options.MaximumThumbnailCreationConcurrency > 0
            && options.MaximumThumbnailSourceImageBytes > 0L
            && options.OrderTimestampIntervalMilliseconds > 0;
    }
}
