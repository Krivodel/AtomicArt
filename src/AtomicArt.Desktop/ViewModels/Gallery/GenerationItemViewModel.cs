using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed partial class GenerationItemViewModel :
    ObservableObject,
    IGalleryItemViewModel,
    IGalleryItemStateSource
{
    public Guid? CorrelationId { get; private set; }
    public int? GenerationOrdinal { get; private set; }
    public string DisplayImagePath => ImagePath ?? string.Empty;
    public string DisplayThumbnailPath =>
        string.IsNullOrWhiteSpace(ThumbnailPath) ? DisplayImagePath : ThumbnailPath;
    public bool HasDisplayImagePath => !string.IsNullOrWhiteSpace(DisplayImagePath);
    public string Status => StatusDescriptor.DisplayText;
    public bool IsGenerated => StatusDescriptor.VisualState == GenerationItemVisualState.Generated;
    public bool IsGenerating => StatusDescriptor.VisualState == GenerationItemVisualState.Generating;
    public bool IsFailed => StatusDescriptor.VisualState == GenerationItemVisualState.Failed;
    public bool ShowsGeneratedImage => HasDisplayImagePath && !IsFailed;
    public bool ShowsGenerationProgress => IsGenerating && !HasDisplayImagePath && !IsFailed;
    public bool ShowsEmptyPreview => !ShowsGeneratedImage && !ShowsGenerationProgress && !IsFailed;
    public string DeleteOrCancelGlyph => IsGenerating ? UiStrings.CancelGlyph : UiStrings.DeleteGlyph;
    private IGenerationItemStatusDescriptor StatusDescriptor => _statusDescriptorRegistry.Get(StatusKind);

    private readonly IGenerationItemStatusDescriptorRegistry _statusDescriptorRegistry;

    [ObservableProperty]
    private Guid _id;
    [ObservableProperty]
    private string _modelId = string.Empty;
    [ObservableProperty]
    private string _modelDisplayName = string.Empty;
    [ObservableProperty]
    private string _prompt = string.Empty;
    [ObservableProperty]
    private string _resolution = string.Empty;
    [ObservableProperty]
    private string _aspectRatio = string.Empty;
    [ObservableProperty]
    private DateTime _createdAtUtc;
    [ObservableProperty]
    private DateTime? _completedAtUtc;
    [ObservableProperty]
    private TimeSpan? _generationDuration;
    [ObservableProperty]
    private GenerationPriceDto? _price;
    [ObservableProperty]
    private GenerationUsageDto? _usage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayImagePath))]
    [NotifyPropertyChangedFor(nameof(DisplayThumbnailPath))]
    [NotifyPropertyChangedFor(nameof(HasDisplayImagePath))]
    [NotifyPropertyChangedFor(nameof(ShowsGeneratedImage))]
    [NotifyPropertyChangedFor(nameof(ShowsGenerationProgress))]
    [NotifyPropertyChangedFor(nameof(ShowsEmptyPreview))]
    private string? _imagePath;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayThumbnailPath))]
    private string? _thumbnailPath;
    [ObservableProperty]
    private string _elapsedText = string.Empty;
    [ObservableProperty]
    private int _attachedImagesCount;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status))]
    [NotifyPropertyChangedFor(nameof(IsGenerated))]
    [NotifyPropertyChangedFor(nameof(IsGenerating))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(ShowsGeneratedImage))]
    [NotifyPropertyChangedFor(nameof(ShowsGenerationProgress))]
    [NotifyPropertyChangedFor(nameof(ShowsEmptyPreview))]
    [NotifyPropertyChangedFor(nameof(DeleteOrCancelGlyph))]
    private GenerationItemStatus _statusKind;

    public GenerationItemViewModel(
        GenerationItemDto item,
        int attachedImagesCount,
        string? imagePath,
        IGenerationItemStatusDescriptorRegistry statusDescriptorRegistry)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(statusDescriptorRegistry);

        _statusDescriptorRegistry = statusDescriptorRegistry;

        Id = item.Id;
        ModelId = item.ModelId;
        ModelDisplayName = item.ModelDisplayName;
        Prompt = item.Prompt;
        Resolution = item.Resolution;
        AspectRatio = item.AspectRatio;
        CreatedAtUtc = item.CreatedAtUtc;
        CompletedAtUtc = item.CompletedAtUtc;
        GenerationDuration = item.GenerationDuration;
        Price = item.Price;
        Usage = item.Usage;
        StatusKind = item.Status;
        ImagePath = imagePath;
        AttachedImagesCount = attachedImagesCount;
        RefreshElapsedText(DateTime.UtcNow);
    }

    private GenerationItemViewModel(
        GenerationStartSnapshot start,
        Guid correlationId,
        int generationOrdinal,
        IGenerationItemStatusDescriptorRegistry statusDescriptorRegistry)
    {
        ArgumentNullException.ThrowIfNull(statusDescriptorRegistry);

        _statusDescriptorRegistry = statusDescriptorRegistry;
        Id = Guid.NewGuid();
        ModelId = start.ModelId;
        ModelDisplayName = start.ModelDisplayName;
        Prompt = start.Prompt;
        Resolution = start.Resolution;
        AspectRatio = start.AspectRatio;
        CreatedAtUtc = start.RequestedAtUtc;
        CompletedAtUtc = null;
        GenerationDuration = null;
        Price = null;
        Usage = null;
        StatusKind = GenerationItemStatus.Generating;
        AttachedImagesCount = start.AttachedImagesCount;
        CorrelationId = correlationId;
        GenerationOrdinal = generationOrdinal;
        RefreshElapsedText(DateTime.UtcNow);
    }

    public static GenerationItemViewModel CreatePlaceholder(
        GenerationStartSnapshot start,
        Guid correlationId,
        int generationOrdinal,
        IGenerationItemStatusDescriptorRegistry statusDescriptorRegistry)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(statusDescriptorRegistry);

        return new GenerationItemViewModel(
            start,
            correlationId,
            generationOrdinal,
            statusDescriptorRegistry);
    }

    public static GenerationItemViewModel Restore(
        GalleryItemState state,
        string? imagePath,
        string? thumbnailPath,
        IGenerationItemStatusDescriptorRegistry statusDescriptorRegistry)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(statusDescriptorRegistry);

        GalleryItemState normalizedState = GalleryItemStateMapper.NormalizeForViewModel(
            state,
            imagePath,
            thumbnailPath);
        GenerationItemDto item = GalleryItemStateMapper.ToGenerationItemDto(normalizedState);
        GenerationItemViewModel viewModel = new(
            item,
            normalizedState.AttachedImagesCount,
            normalizedState.ImagePath,
            statusDescriptorRegistry)
        {
            CorrelationId = normalizedState.CorrelationId,
            GenerationOrdinal = normalizedState.GenerationOrdinal,
            ThumbnailPath = normalizedState.ThumbnailPath
        };

        return viewModel;
    }

    public GalleryItemState CreateState()
    {
        return GalleryItemStateMapper.FromSource(this);
    }

    public void UpdateFromResult(
        GenerationItemDto item,
        string? imagePath,
        string? thumbnailPath)
    {
        ArgumentNullException.ThrowIfNull(item);

        Id = item.Id;
        ModelId = item.ModelId;
        ModelDisplayName = item.ModelDisplayName;
        Prompt = item.Prompt;
        Resolution = item.Resolution;
        AspectRatio = item.AspectRatio;
        CreatedAtUtc = item.CreatedAtUtc;
        CompletedAtUtc = item.CompletedAtUtc;
        GenerationDuration = item.GenerationDuration;
        Price = item.Price;
        Usage = item.Usage;
        StatusKind = item.Status;
        ImagePath = imagePath;
        ThumbnailPath = thumbnailPath;
        CorrelationId = null;
        GenerationOrdinal = null;
        RefreshComputedState();
    }

    public void MarkFailed()
    {
        StatusKind = GenerationItemStatus.Failed;
        ImagePath = null;
        ThumbnailPath = null;
        RefreshComputedState();
    }

    public void RefreshElapsedText(DateTime utcNow)
    {
        DateTime createdAtUtc = CreatedAtUtc;

        if (utcNow < createdAtUtc)
        {
            utcNow = createdAtUtc;
        }

        TimeSpan elapsed = utcNow - createdAtUtc;

        int totalSeconds = Math.Max(1, (int)elapsed.TotalSeconds);

        if (totalSeconds < 60)
        {
            ElapsedText = $"{totalSeconds}с";
            return;
        }

        int totalMinutes = Math.Max(1, (int)elapsed.TotalMinutes);

        if (totalMinutes < 60)
        {
            ElapsedText = $"{totalMinutes}м";
            return;
        }

        int totalHours = Math.Max(1, (int)elapsed.TotalHours);

        if (totalHours < 24)
        {
            ElapsedText = $"{totalHours}ч";
            return;
        }

        int fullMonths = (utcNow.Year - createdAtUtc.Year) * 12 + utcNow.Month - createdAtUtc.Month;

        if (createdAtUtc.AddMonths(fullMonths) > utcNow)
        {
            fullMonths--;
        }

        if (fullMonths < 1)
        {
            int totalDays = Math.Max(1, (int)elapsed.TotalDays);
            ElapsedText = $"{totalDays}д";
            return;
        }

        if (fullMonths < 12)
        {
            ElapsedText = $"{fullMonths}мес";
            return;
        }

        int fullYears = fullMonths / 12;
        ElapsedText = $"{fullYears}г";
    }

    private void RefreshComputedState()
    {
        OnPropertyChanged(nameof(DisplayImagePath));
        OnPropertyChanged(nameof(DisplayThumbnailPath));
        OnPropertyChanged(nameof(HasDisplayImagePath));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsGenerated));
        OnPropertyChanged(nameof(IsGenerating));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(ShowsGeneratedImage));
        OnPropertyChanged(nameof(ShowsGenerationProgress));
        OnPropertyChanged(nameof(ShowsEmptyPreview));
        OnPropertyChanged(nameof(DeleteOrCancelGlyph));
    }

}
