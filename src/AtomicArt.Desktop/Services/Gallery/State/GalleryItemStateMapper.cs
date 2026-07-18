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

        GalleryItemState state = new()
        {
            Id = source.Id,
            ModelId = source.ModelId,
            ModelDisplayName = source.ModelDisplayName,
            Prompt = source.Prompt,
            AspectRatio = source.AspectRatio,
            Resolution = source.Resolution,
            CreatedAtUtc = source.CreatedAtUtc,
            Status = source.StatusKind,
            ImagePath = source.ImagePath,
            ThumbnailPath = source.ThumbnailPath,
            CompletedAtUtc = source.CompletedAtUtc,
            GenerationDuration = source.GenerationDuration,
            Price = source.Price,
            Usage = source.Usage,
            AttachedImagesCount = source.AttachedImagesCount,
            CorrelationId = source.CorrelationId,
            GenerationOrdinal = source.GenerationOrdinal
        };

        return NormalizeForStorage(state, ResolveOriginalImagePath, ResolveOriginalThumbnailPath);
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

        GenerationItemStatus status = ResolveStatus(item.Status, statusPolicy);
        string? imagePath = resolveImagePath(item);
        string? thumbnailPath = resolveThumbnailPath(item);

        return new GalleryItemState
        {
            Id = item.Id,
            ModelId = item.ModelId ?? string.Empty,
            ModelDisplayName = item.ModelDisplayName ?? string.Empty,
            Prompt = item.Prompt ?? string.Empty,
            AspectRatio = item.AspectRatio ?? string.Empty,
            Resolution = item.Resolution ?? string.Empty,
            CreatedAtUtc = item.CreatedAtUtc,
            Status = status,
            ImagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath,
            ThumbnailPath = string.IsNullOrWhiteSpace(thumbnailPath) ? null : thumbnailPath,
            CompletedAtUtc = item.CompletedAtUtc,
            GenerationDuration = item.GenerationDuration,
            Price = item.Price,
            Usage = item.Usage,
            AttachedImagesCount = Math.Max(0, item.AttachedImagesCount),
            CorrelationId = ResolveCorrelationId(item, status),
            GenerationOrdinal = ResolveGenerationOrdinal(item, status)
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

    private static Guid? ResolveCorrelationId(
        GalleryItemState item,
        GenerationItemStatus status)
    {
        if (status == GenerationItemStatus.Generating)
        {
            return item.CorrelationId;
        }

        return null;
    }

    private static int? ResolveGenerationOrdinal(
        GalleryItemState item,
        GenerationItemStatus status)
    {
        if (status == GenerationItemStatus.Generating)
        {
            return item.GenerationOrdinal;
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
