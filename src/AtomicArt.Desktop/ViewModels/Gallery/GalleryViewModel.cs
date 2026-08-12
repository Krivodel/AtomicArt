using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.ViewModels.Generation;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed partial class GalleryViewModel :
    ObservableObject,
    IRecipient<LocalizationChangedMessage>,
    IDisposable
{
    public ReadOnlyObservableCollection<GenerationItemViewModel> Items { get; }
    public bool IsEmpty => _itemsController.IsEmpty;
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsSelectionMode => _selectionController.IsActive;
    public int SelectedCount => _selectionController.SelectedCount;
    public string SelectionSummary => _textProvider.Format(
        GalleryLocalizationKeys.SelectedCount,
        SelectedCount);
    public string DeleteSelectedText => _textProvider.Format(
        GalleryLocalizationKeys.DeleteSelected,
        SelectedCount);
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
    private readonly IDialogService _dialogService;
    private readonly IDeletionConfirmationService _deletionConfirmationService;
    private readonly IGalleryItemDeletionService _deletionService;
    private readonly IGalleryStateService _galleryStateService;
    private readonly GalleryLifecycleViewStateController _viewStateController;
    private readonly GalleryItemsController _itemsController;
    private readonly GallerySelectionController _selectionController;
    private readonly GalleryLifecycleController _lifecycleController;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ITextClipboardService _textClipboardService;
    private readonly GenerationPriceFormatter _priceFormatter;
    private readonly GenerationDurationFormatter _durationFormatter;
    private readonly IGenerationCancellationService _generationCancellationService;
    private readonly ILocalizationTextProvider _textProvider;
    private IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? _attachImagesCommand;
    private IGenerationPanelPresetTarget? _generationPanelPresetTarget;
    private GenerationMetadataViewModel? _selectedMetadata;
    private string? _errorLocalizationKey;
    [ObservableProperty]
    private bool _isMetadataOpen;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    [ObservableProperty]
    private bool _isLoading;

    public GalleryViewModel(
        IFileRevealService fileRevealService,
        IImageViewerService imageViewerService,
        IDialogService dialogService,
        IDeletionConfirmationService deletionConfirmationService,
        IGalleryItemDeletionService deletionService,
        IGalleryStateService galleryStateService,
        GalleryLifecycleViewStateController viewStateController,
        GalleryItemsController itemsController,
        GalleryLifecycleController lifecycleController,
        IViewModelErrorHandler errorHandler,
        ITextClipboardService textClipboardService,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter,
        IMessenger messenger,
        ILocalizationTextProvider textProvider,
        IGenerationCancellationService? generationCancellationService = null)
    {
        ArgumentNullException.ThrowIfNull(fileRevealService);
        ArgumentNullException.ThrowIfNull(imageViewerService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(deletionConfirmationService);
        ArgumentNullException.ThrowIfNull(deletionService);
        ArgumentNullException.ThrowIfNull(galleryStateService);
        ArgumentNullException.ThrowIfNull(viewStateController);
        ArgumentNullException.ThrowIfNull(itemsController);
        ArgumentNullException.ThrowIfNull(lifecycleController);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(textClipboardService);
        ArgumentNullException.ThrowIfNull(priceFormatter);
        ArgumentNullException.ThrowIfNull(durationFormatter);
        ArgumentNullException.ThrowIfNull(messenger);
        ArgumentNullException.ThrowIfNull(textProvider);

        _fileRevealService = fileRevealService;
        _imageViewerService = imageViewerService;
        _dialogService = dialogService;
        _deletionConfirmationService = deletionConfirmationService;
        _deletionService = deletionService;
        _galleryStateService = galleryStateService;
        _viewStateController = viewStateController;
        _itemsController = itemsController;
        Items = _itemsController.Items;
        _itemsController.IsEmptyChanged += OnItemsEmptyChanged;
        _selectionController = new GallerySelectionController(Items);
        _selectionController.StateChanged += OnSelectionStateChanged;
        _lifecycleController = lifecycleController;
        _errorHandler = errorHandler;
        _textClipboardService = textClipboardService;
        _priceFormatter = priceFormatter;
        _durationFormatter = durationFormatter;
        _textProvider = textProvider;
        _generationCancellationService = generationCancellationService
            ?? NullGenerationCancellationService.Instance;
        messenger.Register<LocalizationChangedMessage>(this);
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

    public async Task RebaseDataRootAsync(
        string sourceRootDirectory,
        string destinationRootDirectory,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootDirectory);

        SelectedMetadata = null;
        IsMetadataOpen = false;
        await _viewStateController.RebaseDataRootPathsAsync(
            sourceRootDirectory,
            destinationRootDirectory,
            ct);
        IReadOnlyList<GalleryItemState> snapshot = _itemsController.CreateStateSnapshot();
        await _galleryStateService.SaveAsync(snapshot, ct);
    }

    public void Receive(LocalizationChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        DateTime utcNow = DateTime.UtcNow;

        foreach (GenerationItemViewModel item in Items)
        {
            item.RefreshLocalization(utcNow);
        }

        SelectedMetadata?.RefreshLocalization();

        if (_errorLocalizationKey is not null)
        {
            ErrorMessage = _textProvider.Get(_errorLocalizationKey);
        }

        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(DeleteSelectedText));
    }

    public void Dispose()
    {
        SelectedMetadata = null;
        _itemsController.IsEmptyChanged -= OnItemsEmptyChanged;
        _selectionController.StateChanged -= OnSelectionStateChanged;
        _selectionController.Dispose();

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

    private static LocalizedConfirmationDialogRequest CreateDeletionConfirmationRequest(
        int itemCount)
    {
        object?[] messageArguments = [itemCount];

        return new LocalizedConfirmationDialogRequest(
            GalleryLocalizationKeys.DeletionConfirmationTitle,
            GalleryLocalizationKeys.DeletionConfirmationMessage,
            GalleryLocalizationKeys.ConfirmDeletion,
            CommonLocalizationKeys.Cancel,
            ConfirmationDialogKind.Destructive,
            ConfirmationDialogBackgroundClickBehavior.Dismiss,
            messageArguments);
    }

    private async Task ExecuteConfirmedDeletionAsync(
        int itemCount,
        Func<CancellationToken, Task> deletion,
        CancellationToken ct)
    {
        if (_deletionConfirmationService.IsConfirmationRequired)
        {
            LocalizedConfirmationDialogRequest request =
                CreateDeletionConfirmationRequest(itemCount);
            bool isConfirmed = await _dialogService.ShowConfirmationAsync(
                request,
                ct);

            if (!isConfirmed)
            {
                return;
            }
        }

        await deletion(ct);
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private Task RevealInFolderAsync(
        GenerationItemViewModel? item,
        CancellationToken ct)
    {
        return RevealInFolderCoreAsync(
            item,
            FileRevealWindowMode.ReuseExisting,
            nameof(RevealInFolderAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private Task RevealInNewFolderWindowAsync(
        GenerationItemViewModel? item,
        CancellationToken ct)
    {
        return RevealInFolderCoreAsync(
            item,
            FileRevealWindowMode.OpenNew,
            nameof(RevealInNewFolderWindowAsync),
            ct);
    }

    private async Task RevealInFolderCoreAsync(
        GenerationItemViewModel? item,
        FileRevealWindowMode windowMode,
        string operationName,
        CancellationToken ct)
    {
        if (IsLoading)
        {
            return;
        }

        await ExecuteLoadingUserOperationAsync(
            operationCt => _fileRevealService.RevealAsync(
                item?.ImagePath,
                item?.ModelId ?? string.Empty,
                windowMode,
                operationCt),
            operationName,
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
            ShowFailureDetailsCommand,
            ReuseGenerationCommand,
            _textClipboardService,
            _errorHandler,
            _priceFormatter,
            _durationFormatter,
            _textProvider);
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

                await ExecuteConfirmedDeletionAsync(
                    1,
                    async deletionCt =>
                    {
                        CancelGenerationIfActive(item);
                        await DeleteItemAsync(item, deletionCt);
                    },
                    operationCt);
            },
            nameof(DeleteOrCancelAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanChangeSelection))]
    private void ToggleSelection(GenerationItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _selectionController.Toggle(item);
    }

    [RelayCommand(CanExecute = nameof(CanChangeSelection))]
    private void SelectRange(GenerationItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _selectionController.SelectRange(item);
    }

    [RelayCommand(CanExecute = nameof(CanSelectAll))]
    private void SelectAll()
    {
        _selectionController.SelectAll();
    }

    [RelayCommand(CanExecute = nameof(CanExitSelectionMode))]
    private void ExitSelectionMode()
    {
        _selectionController.Exit();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync(CancellationToken ct)
    {
        if (!CanDeleteSelected())
        {
            return;
        }

        IReadOnlyList<GenerationItemViewModel> selectedItems =
            _selectionController.GetSelectedItems();

        await ExecuteLoadingUserOperationAsync(
            operationCt => ExecuteConfirmedDeletionAsync(
                selectedItems.Count,
                async deletionCt =>
                {
                    IReadOnlyList<GenerationItemViewModel> existingSelectedItems =
                        selectedItems
                            .Where(_itemsController.Contains)
                            .ToList();

                    if (existingSelectedItems.Count == 0)
                    {
                        return;
                    }

                    await DeleteItemsAsync(existingSelectedItems, deletionCt);
                },
                operationCt),
            nameof(DeleteSelectedAsync),
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

    private bool CanRunCommand()
    {
        return !IsLoading && !IsSelectionMode;
    }

    private bool CanChangeSelection(GenerationItemViewModel? item)
    {
        return !IsLoading
            && item is not null
            && _itemsController.Contains(item);
    }

    private bool CanSelectAll()
    {
        return !IsLoading
            && !IsEmpty
            && SelectedCount < Items.Count;
    }

    private bool CanExitSelectionMode()
    {
        return !IsLoading && IsSelectionMode;
    }

    private bool CanDeleteSelected()
    {
        return !IsLoading && IsSelectionMode && SelectedCount > 0;
    }

    private bool CanOpenViewer(GenerationItemViewModel? item)
    {
        return !IsLoading
            && !IsSelectionMode
            && item is { ShowsGeneratedImage: true }
            && !string.IsNullOrWhiteSpace(item.ImagePath);
    }

    private bool CanShowFailureDetails(GenerationItemViewModel? item)
    {
        return !IsLoading
            && !IsSelectionMode
            && item is { IsFailed: true };
    }

    [RelayCommand(CanExecute = nameof(CanOpenViewer), AllowConcurrentExecutions = true)]
    private async Task OpenViewerAsync(GenerationItemViewModel? item, CancellationToken ct)
    {
        if (item is null || !CanOpenViewer(item))
        {
            return;
        }

        ClearErrorMessage();
        await ExecuteUserOperationAsync(
            operationCt => OpenViewerCoreAsync(item, operationCt),
            nameof(OpenViewerAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanShowFailureDetails))]
    private async Task ShowFailureDetailsAsync(
        GenerationItemViewModel? item,
        CancellationToken ct)
    {
        if (item is null || !CanShowFailureDetails(item))
        {
            return;
        }

        string localizationKey =
            GenerationFailureMessageResolver.GetLocalizationKey(item.FailureCode);
        await _dialogService.ShowLocalizedErrorAsync(localizationKey, ct);
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

    private async Task DeleteItemsAsync(
        IReadOnlyList<GenerationItemViewModel> items,
        CancellationToken ct)
    {
        IReadOnlyList<GalleryItemDeletionRequest> deletionRequests = items
            .Select(CreateDeletionRequest)
            .ToList();

        IReadOnlyList<Guid> activeGenerationIds = items
            .Where(item => item.IsGenerating && item.CorrelationId.HasValue)
            .Select(item => item.CorrelationId.GetValueOrDefault())
            .Distinct()
            .ToList();

        foreach (Guid activeGenerationId in activeGenerationIds)
        {
            _generationCancellationService.Cancel(activeGenerationId);
        }

        _selectionController.Exit();
        await _viewStateController.RemoveItemsAsync(items, ct);
        await _deletionService.DeleteFilesAsync(deletionRequests, ct);
        IReadOnlyList<GalleryItemState> snapshot = _itemsController.CreateStateSnapshot();
        await _galleryStateService.SaveAsync(snapshot, ct);
    }

    private void CancelGenerationIfActive(GenerationItemViewModel item)
    {
        if (item.IsGenerating
            && item.CorrelationId is Guid logicalGenerationId)
        {
            _generationCancellationService.Cancel(logicalGenerationId);
        }
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
            localizationKey => _errorLocalizationKey = localizationKey,
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
            ClearErrorMessage();
            await ExecuteUserOperationAsync(operation, operationName, ct);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ClearErrorMessage()
    {
        _errorLocalizationKey = null;
        ErrorMessage = null;
    }

    private void NotifyInteractiveCommandsCanExecuteChanged()
    {
        RevealInFolderCommand.NotifyCanExecuteChanged();
        RevealInNewFolderWindowCommand.NotifyCanExecuteChanged();
        OpenViewerCommand.NotifyCanExecuteChanged();
        ShowFailureDetailsCommand.NotifyCanExecuteChanged();
        DeleteOrCancelCommand.NotifyCanExecuteChanged();
        ToggleSelectionCommand.NotifyCanExecuteChanged();
        SelectRangeCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        ExitSelectionModeCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        _ = value;
        NotifyInteractiveCommandsCanExecuteChanged();
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

    private void OnSelectionStateChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;

        OnPropertyChanged(nameof(IsSelectionMode));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(DeleteSelectedText));
        NotifyInteractiveCommandsCanExecuteChanged();
    }
}
