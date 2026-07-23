using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed partial class GalleryViewModel : ObservableObject, IDisposable
{
    public ReadOnlyObservableCollection<GenerationItemViewModel> Items { get; }
    public bool IsEmpty => _itemsController.IsEmpty;
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public GenerationMetadataViewModel? SelectedMetadata
    {
        get => _selectedMetadata;
        private set
        {
            GenerationMetadataViewModel? previous = _selectedMetadata;

            if (!SetProperty(ref _selectedMetadata, value))
            {
                return;
            }

            previous?.Dispose();
        }
    }

    private readonly IFileRevealService _fileRevealService;
    private readonly IImageViewerService _imageViewerService;
    private readonly IGalleryItemDeletionService _deletionService;
    private readonly IGalleryStateService _galleryStateService;
    private readonly GalleryLifecycleViewStateController _viewStateController;
    private readonly GalleryItemsController _itemsController;
    private readonly GalleryLifecycleController _lifecycleController;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ITextClipboardService _textClipboardService;
    private readonly GenerationPriceFormatter _priceFormatter;
    private readonly GenerationDurationFormatter _durationFormatter;
    private readonly IGenerationCancellationService _generationCancellationService;
    private IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? _attachImagesCommand;
    private IGenerationPanelPresetTarget? _generationPanelPresetTarget;
    private GenerationMetadataViewModel? _selectedMetadata;
    [ObservableProperty]
    private bool _isMetadataOpen;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevealInFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenViewerCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteOrCancelCommand))]
    private bool _isLoading;

    public GalleryViewModel(
        IFileRevealService fileRevealService,
        IImageViewerService imageViewerService,
        IGalleryItemDeletionService deletionService,
        IGalleryStateService galleryStateService,
        GalleryLifecycleViewStateController viewStateController,
        GalleryItemsController itemsController,
        GalleryLifecycleController lifecycleController,
        IViewModelErrorHandler errorHandler,
        ITextClipboardService textClipboardService,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter,
        IGenerationCancellationService? generationCancellationService = null)
    {
        ArgumentNullException.ThrowIfNull(fileRevealService);
        ArgumentNullException.ThrowIfNull(imageViewerService);
        ArgumentNullException.ThrowIfNull(deletionService);
        ArgumentNullException.ThrowIfNull(galleryStateService);
        ArgumentNullException.ThrowIfNull(viewStateController);
        ArgumentNullException.ThrowIfNull(itemsController);
        ArgumentNullException.ThrowIfNull(lifecycleController);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(textClipboardService);
        ArgumentNullException.ThrowIfNull(priceFormatter);
        ArgumentNullException.ThrowIfNull(durationFormatter);

        _fileRevealService = fileRevealService;
        _imageViewerService = imageViewerService;
        _deletionService = deletionService;
        _galleryStateService = galleryStateService;
        _viewStateController = viewStateController;
        _itemsController = itemsController;
        Items = _itemsController.Items;
        _itemsController.IsEmptyChanged += OnItemsEmptyChanged;
        _lifecycleController = lifecycleController;
        _errorHandler = errorHandler;
        _textClipboardService = textClipboardService;
        _priceFormatter = priceFormatter;
        _durationFormatter = durationFormatter;
        _generationCancellationService = generationCancellationService
            ?? NullGenerationCancellationService.Instance;
    }

    public void ConfigureImageViewerAttachments(
        IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachImagesCommand)
    {
        ArgumentNullException.ThrowIfNull(attachImagesCommand);

        _attachImagesCommand = attachImagesCommand;
    }

    public void ConfigureGenerationPresetTarget(IGenerationPanelPresetTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_generationPanelPresetTarget is not null)
        {
            _generationPanelPresetTarget.PresetAvailabilityChanged -=
                OnGenerationPresetAvailabilityChanged;
        }

        _generationPanelPresetTarget = target;
        _generationPanelPresetTarget.PresetAvailabilityChanged +=
            OnGenerationPresetAvailabilityChanged;
        ReuseGenerationCommand.NotifyCanExecuteChanged();
    }

    public void AddGeneratedItems(IReadOnlyList<GenerationItemDto> items, int attachedImagesCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        IReadOnlyList<GenerationItemViewModel> addedItems =
            _itemsController.CreateGeneratedItems(items, attachedImagesCount);
        _itemsController.AddGeneratedItems(addedItems);
        ObserveGalleryOperation(
            ct => _viewStateController.GenerateFrontAsync(addedItems, ct),
            CancellationToken.None);
    }

    public Task RestoreStateAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        return _viewStateController.RestoreAsync(items, ct);
    }

    public void Dispose()
    {
        SelectedMetadata = null;
        _itemsController.IsEmptyChanged -= OnItemsEmptyChanged;

        if (_generationPanelPresetTarget is not null)
        {
            _generationPanelPresetTarget.PresetAvailabilityChanged -=
                OnGenerationPresetAvailabilityChanged;
        }

        _lifecycleController.Dispose();
    }

    private static GenerationPanelPreset CreateGenerationPanelPreset(
        GenerationItemViewModel item)
    {
        return new GenerationPanelPreset(
            item.ModelId,
            item.Prompt,
            item.AspectRatio,
            item.Resolution);
    }

    private static GalleryItemDeletionRequest CreateDeletionRequest(GenerationItemViewModel item)
    {
        return new GalleryItemDeletionRequest(
            item.Id,
            item.ModelId,
            item.ImagePath,
            item.ThumbnailPath);
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task RevealInFolderAsync(GenerationItemViewModel? item, CancellationToken ct)
    {
        if (IsLoading)
        {
            return;
        }

        await ExecuteLoadingUserOperationAsync(
            operationCt => _fileRevealService.RevealAsync(
                item?.ImagePath,
                item?.ModelId ?? string.Empty,
                operationCt),
            nameof(RevealInFolderAsync),
            ct);
    }

    [RelayCommand]
    private void OpenMetadata(GenerationItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedMetadata = GenerationMetadataViewModel.FromItem(
            item,
            CloseOverlayCommand,
            OpenViewerCommand,
            ReuseGenerationCommand,
            _textClipboardService,
            _errorHandler,
            _priceFormatter,
            _durationFormatter);
        IsMetadataOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task DeleteOrCancelAsync(GenerationItemViewModel? item, CancellationToken ct)
    {
        if (IsLoading)
        {
            return;
        }

        if (item is null)
        {
            return;
        }

        await ExecuteLoadingUserOperationAsync(
            async operationCt =>
            {
                if (!_itemsController.Contains(item))
                {
                    return;
                }

                if (item.IsGenerating
                    && item.CorrelationId is Guid logicalGenerationId)
                {
                    _generationCancellationService.Cancel(logicalGenerationId);
                }

                await DeleteItemAsync(item, operationCt);
            },
            nameof(DeleteOrCancelAsync),
            ct);
    }

    [RelayCommand]
    private void CloseOverlay()
    {
        IsMetadataOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanReuseGeneration))]
    private void ReuseGeneration(GenerationItemViewModel? item)
    {
        IGenerationPanelPresetTarget? target = _generationPanelPresetTarget;
        if (item is null || target is null)
        {
            return;
        }

        GenerationPanelPreset preset = CreateGenerationPanelPreset(item);
        if (!target.CanApplyPreset(preset))
        {
            return;
        }

        target.ApplyPreset(preset);
        IsMetadataOpen = false;
    }

    private bool CanReuseGeneration(GenerationItemViewModel? item)
    {
        IGenerationPanelPresetTarget? target = _generationPanelPresetTarget;

        return item is not null
            && target is not null
            && target.CanApplyPreset(CreateGenerationPanelPreset(item));
    }

    private void OnItemsEmptyChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void OnItemsEmptyChanged(object? sender, EventArgs args)
    {
        OnItemsEmptyChanged();
    }

    private void OnGenerationPresetAvailabilityChanged(object? sender, EventArgs args)
    {
        ReuseGenerationCommand.NotifyCanExecuteChanged();
    }

    private bool CanRunCommand()
    {
        return !IsLoading;
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand), AllowConcurrentExecutions = true)]
    private async Task OpenViewerAsync(GenerationItemViewModel? item, CancellationToken ct)
    {
        if (IsLoading)
        {
            return;
        }

        if (item is null)
        {
            return;
        }

        ErrorMessage = null;
        await ExecuteUserOperationAsync(
            operationCt => OpenViewerCoreAsync(item, operationCt),
            nameof(OpenViewerAsync),
            ct);
    }

    private async Task DeleteItemAsync(GenerationItemViewModel item, CancellationToken ct)
    {
        GalleryItemDeletionRequest deletionRequest = CreateDeletionRequest(item);
        Guid removedItemId = item.Id;
        _itemsController.Delete(item);
        await _viewStateController.RemoveAsync(removedItemId, ct);
        await _deletionService.DeleteFilesAsync(deletionRequest, ct);
        IReadOnlyList<GalleryItemState> snapshot = _itemsController.CreateStateSnapshot();
        await _galleryStateService.SaveAsync(snapshot, ct);
    }

    private async Task OpenViewerCoreAsync(GenerationItemViewModel item, CancellationToken ct)
    {
        GalleryImageViewerRequest? request = CreateImageViewerRequestOrDefault(item);

        if (request is null)
        {
            return;
        }

        await _imageViewerService.OpenAsync(request, ct);
    }

    private GalleryImageViewerRequest? CreateImageViewerRequestOrDefault(GenerationItemViewModel selectedItem)
    {
        List<GalleryImageViewerItem> viewerItems = [];

        foreach (GenerationItemViewModel item in Items)
        {
            if (!item.ShowsGeneratedImage || string.IsNullOrWhiteSpace(item.ImagePath))
            {
                continue;
            }

            viewerItems.Add(new GalleryImageViewerItem(
                item.Id,
                new GalleryFileImageViewerSource(
                    item.ModelId,
                    item.ImagePath,
                    item.ThumbnailPath)));
        }

        if (!viewerItems.Any(item => item.Id == selectedItem.Id))
        {
            return null;
        }

        return new GalleryImageViewerRequest(
            new GalleryStaticImageViewerItemsSource(viewerItems),
            selectedItem.Id,
            _attachImagesCommand);
    }

    private void ObserveGalleryOperation(
        Func<CancellationToken, Task> operation,
        CancellationToken ct)
    {
        _ = ObserveGalleryOperationAsync(operation, ct);
    }

    private async Task ObserveGalleryOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct)
    {
        try
        {
            await operation(ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (InvalidOperationException ex)
        {
            _errorHandler.Log(ex, nameof(ObserveGalleryOperationAsync));
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(ObserveGalleryOperationAsync));
        }
    }

    private Task ExecuteUserOperationAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken ct)
    {
        return ViewModelAsyncOperation.ExecuteAsync(
            _errorHandler,
            errorMessage => ErrorMessage = errorMessage,
            operation,
            operationName,
            ct);
    }

    private async Task ExecuteLoadingUserOperationAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            await ExecuteUserOperationAsync(operation, operationName, ct);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
