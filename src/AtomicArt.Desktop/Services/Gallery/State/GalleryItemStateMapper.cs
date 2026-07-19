using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Gallery.State;

internal static class GalleryItemStateMapper
{
    public static bool IsValid(GalleryItemState? item)
    {
        return item is not null
            && item.Id != Guid.Empty
            && !string.IsNullOrWhiteSpace(item.ModelId)
            && !string.IsNullOrWhiteSpace(item.ModelDisplayName)
            && !string.IsNullOrWhiteSpace(item.AspectRatio)
            && !string.IsNullOrWhiteSpace(item.Resolution)
            && item.CreatedAtUtc != default
            && item.AttachedImagesCount >= 0
            && IsKnownStatus(item.Status);
    }

    public static GalleryItemState FromSource(IGalleryItemStateSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return NormalizeSource(
            source,
            source => source.ImagePath,
            source => source.ThumbnailPath,
            GalleryItemStateStatusPolicy.PreserveStatus);
    }

    public static GalleryItemState NormalizeForDeserialization(GalleryItemState item)
    {
        return NormalizeForStorage(item, ResolveOriginalImagePath, ResolveOriginalThumbnailPath);
    }

    public static GalleryItemState NormalizeForStorage(
        GalleryItemState item,
        Func<GalleryItemState, string?> resolveImagePath)
    {
        return NormalizeForStorage(item, resolveImagePath, ResolveOriginalThumbnailPath);
    }

    public static GalleryItemState NormalizeForStorage(
        GalleryItemState item,
        Func<GalleryItemState, string?> resolveImagePath,
        Func<GalleryItemState, string?> resolveThumbnailPath)
    {
        return Normalize(
            item,
            resolveImagePath,
            resolveThumbnailPath,
            GalleryItemStateStatusPolicy.PreserveStatus);
    }

    public static GalleryItemState NormalizeForRestore(
        GalleryItemState item,
        Func<GalleryItemState, string?> resolveImagePath)
    {
        return NormalizeForRestore(item, resolveImagePath, ResolveOriginalThumbnailPath);
    }

    public static GalleryItemState NormalizeForRestore(
        GalleryItemState item,
        Func<GalleryItemState, string?> resolveImagePath,
        Func<GalleryItemState, string?> resolveThumbnailPath)
    {
        return Normalize(
            item,
            resolveImagePath,
            resolveThumbnailPath,
            GalleryItemStateStatusPolicy.MarkGeneratingAsFailed);
    }

    public static GalleryItemState NormalizeForViewModel(
        GalleryItemState item,
        string? imagePath,
        string? thumbnailPath)
    {
        return NormalizeForStorage(item, _ => imagePath, _ => thumbnailPath);
    }

    public static GenerationItemDto ToGenerationItemDto(GalleryItemState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new GenerationItemDto(
            state.Id,
            state.ModelId,
            state.ModelDisplayName,
            state.Prompt,
            state.AspectRatio,
            state.Resolution,
            state.CreatedAtUtc,
            state.Status,
            state.ImagePath,
            null,
            state.CompletedAtUtc,
            state.GenerationDuration,
            state.Price,
            state.Usage);
    }

    private static GalleryItemState Normalize(
        GalleryItemState item,
        Func<GalleryItemState, string?> resolveImagePath,
        Func<GalleryItemState, string?> resolveThumbnailPath,
        GalleryItemStateStatusPolicy statusPolicy)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(resolveImagePath);
        ArgumentNullException.ThrowIfNull(resolveThumbnailPath);

        return NormalizeSource(
            item,
            source => resolveImagePath((GalleryItemState)source),
            source => resolveThumbnailPath((GalleryItemState)source),
            statusPolicy);
    }

    private static GalleryItemState NormalizeSource(
        IGalleryItemStateSource source,
        Func<IGalleryItemStateSource, string?> resolveImagePath,
        Func<IGalleryItemStateSource, string?> resolveThumbnailPath,
        GalleryItemStateStatusPolicy statusPolicy)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resolveImagePath);
        ArgumentNullException.ThrowIfNull(resolveThumbnailPath);

        GenerationItemStatus status = ResolveStatus(source.StatusKind, statusPolicy);
        string? imagePath = resolveImagePath(source);
        string? thumbnailPath = resolveThumbnailPath(source);

        return new GalleryItemState
        {
            Id = source.Id,
            ModelId = source.ModelId ?? string.Empty,
            ModelDisplayName = source.ModelDisplayName ?? string.Empty,
            Prompt = source.Prompt ?? string.Empty,
            AspectRatio = source.AspectRatio ?? string.Empty,
            Resolution = source.Resolution ?? string.Empty,
            CreatedAtUtc = source.CreatedAtUtc,
            Status = status,
            ImagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath,
            ThumbnailPath = string.IsNullOrWhiteSpace(thumbnailPath) ? null : thumbnailPath,
            CompletedAtUtc = source.CompletedAtUtc,
            GenerationDuration = source.GenerationDuration,
            Price = source.Price,
            Usage = source.Usage,
            AttachedImagesCount = Math.Max(0, source.AttachedImagesCount),
            CorrelationId = ResolveGeneratingValue(source, status, source => source.CorrelationId),
            GenerationOrdinal = ResolveGeneratingValue(source, status, source => source.GenerationOrdinal)
        };
    }

    private static GenerationItemStatus ResolveStatus(
        GenerationItemStatus status,
        GalleryItemStateStatusPolicy statusPolicy)
    {
        if (!IsKnownStatus(status))
        {
            return GenerationItemStatus.Failed;
        }

        if ((statusPolicy == GalleryItemStateStatusPolicy.MarkGeneratingAsFailed)
            && (status == GenerationItemStatus.Generating))
        {
            return GenerationItemStatus.Failed;
        }

        return status;
    }

    private static T? ResolveGeneratingValue<T>(
        IGalleryItemStateSource item,
        GenerationItemStatus status,
        Func<IGalleryItemStateSource, T?> resolveValue)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(resolveValue);

        if (status == GenerationItemStatus.Generating)
        {
            return resolveValue(item);
        }

        return null;
    }

    private static string? ResolveOriginalImagePath(GalleryItemState item)
    {
        return item.ImagePath;
    }

    private static string? ResolveOriginalThumbnailPath(GalleryItemState item)
    {
        return item.ThumbnailPath;
    }

    private static bool IsKnownStatus(GenerationItemStatus status)
    {
        return Enum.IsDefined(status);
    }
}
