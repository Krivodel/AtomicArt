using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class UniversalNanoBananaPanelViewModel :
    ObservableObject,
    IModelPanelViewModel,
    IAppStateGenerationPanelRestoreTarget,
    IAppStateGenerationPanelFlushTarget,
    IDisposable
{
    public ReadOnlyObservableCollection<ImageModelOption> AvailableModels { get; }
    public IReadOnlyList<string> AspectRatios => SelectedModel?.AspectRatios ?? [];
    public IReadOnlyList<string> Resolutions => SelectedModel?.Resolutions ?? [];
    public IReadOnlyList<int> GenerationCounts => SelectedModel?.GenerationCounts ?? [];
    public double MinimumTemperature => SelectedModel?.Temperature.Minimum ?? 0d;
    public double MaximumTemperature => SelectedModel?.Temperature.Maximum ?? 0d;
    public double DefaultTemperature => SelectedModel?.Temperature.Default ?? 0d;
    public double TemperatureStep => SelectedModel?.Temperature.Step ?? 1d;
    public string TemperatureText => NanoBanana2PanelTextFormatter.FormatTemperatureText(Temperature);
    public IReadOnlyList<GenerationModelThinkingLevelMetadataDto> ThinkingLevels =>
        SelectedModel?.Thinking?.Levels ?? [];
    public bool SupportsThinkingLevel => SelectedModel?.Thinking is not null;
    public ReadOnlyObservableCollection<AttachedImageViewModel> AttachedImages => _attachmentsViewModel.AttachedImages;
    public NanoBanana2QuoteViewModel Quote { get; }
    public string PanelId => GenerationPanelIds.NanoBanana;
    public string ModelId => SelectedModel?.Id ?? string.Empty;
    public string DisplayName => SelectedModel?.DisplayName ?? string.Empty;
    public int MaxAttachedImageBytes => SelectedModel?.MaxAttachedImageBytes ?? 0;
    public int AttachmentInputByteLimit => SelectedModel is null
        ? 0
        : (int)Math.Min(int.MaxValue, SelectedModel.MaxTotalAttachedImageBytes);
    public string AttachmentCounterText => NanoBanana2PanelTextFormatter.FormatAttachmentCounterText(
        AttachedImages.Count,
        SelectedModel?.MaxAttachedImages ?? 0);
    public string GenerateButtonText => Quote.GenerateButtonText;
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsAttaching => _attachmentsViewModel.HasPendingAttachments;
    public bool HasLoadedCatalog => _imageModelOptionCatalog.IsLoaded
                                    && SelectedModel is not null
                                    && !IsCatalogLoading;
    public ImageModelOption? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value))
            {
                OnSelectedModelChanged(value);
            }
        }
    }

    public event EventHandler<PropertyChangedEventArgs>? SelectionValueReset;

    private const string SelectedModelNotInitializedMessage =
        "Selected model is not initialized.";

    private static readonly TimeSpan PromptStateSaveDelay = StateWritePolicy.DeferredWriteDelay;

    private bool CanRunCommand => HasLoadedCatalog
                                  && !IsAttaching
                                  && !string.IsNullOrWhiteSpace(Prompt);
    private bool CanLoadModelCatalog => !IsLoading && !IsCatalogLoading && !_imageModelOptionCatalog.IsLoaded;
    private bool CanPickImage => !IsLoading && HasLoadedCatalog && HasAttachmentCapacity();

    private readonly IGenerationModelCatalogApiClient _generationModelCatalogApiClient;
    private readonly IImageModelOptionCatalog _imageModelOptionCatalog;
    private readonly IApiEndpointService _apiEndpointService;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly IFilePickerService _filePickerService;
    private readonly ISecretStore _secretStore;
    private readonly UniversalNanoBananaPanelModelScope _modelScope;
    private readonly NanoBanana2AttachmentsViewModel _attachmentsViewModel;
    private readonly INanoBanana2GenerationRunner _generationRunner;
    private readonly IGenerationPanelStateService _generationPanelStateService;
    private readonly IImageViewerService _imageViewerService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly CancellationTokenSource _disposeCancellationSource = new();
    private readonly ObservableCollection<ImageModelOption> _availableModels = [];
    private string? _rememberedThinkingLevelValue;
    private ImageModelOption? _selectedModel;
    private CancellationTokenSource? _promptStateSaveCancellation;
    private CancellationTokenSource? _catalogReloadCancellation;
    private long _catalogLoadOperationId;
    private bool _acceptApiEndpointChanges;
    private bool _isDisposed;
    private bool _suppressPricePreviewRefresh;
    private bool _suppressPanelStateSave;
    private bool _suppressSelectionValueResetNotifications;
    private bool _hasRestoredPanelState;
    private bool _hasTemperatureValue;
    [ObservableProperty]
    private string _selectedAspectRatio = string.Empty;
    [ObservableProperty]
    private string _selectedResolution = string.Empty;
    [ObservableProperty]
    private int _generationCount;
    [ObservableProperty]
    private double _temperature;
    [ObservableProperty]
    private GenerationModelThinkingLevelMetadataDto? _selectedThinkingLevel;
    [ObservableProperty]
    private string _prompt = string.Empty;
    [ObservableProperty]
    private bool _isLoading;
    [ObservableProperty]
    private bool _isCatalogLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    public UniversalNanoBananaPanelViewModel(
        IFilePickerService filePickerService,
        ISecretStore secretStore,
        IGenerationModelCatalogApiClient generationModelCatalogApiClient,
        IImageModelOptionCatalog imageModelOptionCatalog,
        IApiEndpointService apiEndpointService,
        IUiThreadDispatcher uiThreadDispatcher,
        UniversalNanoBananaPanelModelScope modelScope,
        NanoBanana2AttachmentsViewModel attachmentsViewModel,
        INanoBanana2GenerationRunner generationRunner,
        IGenerationPanelStateService generationPanelStateService,
        IImageViewerService imageViewerService,
        NanoBanana2QuoteViewModel quote,
        IViewModelErrorHandler errorHandler)
    {
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(generationModelCatalogApiClient);
        ArgumentNullException.ThrowIfNull(imageModelOptionCatalog);
        ArgumentNullException.ThrowIfNull(apiEndpointService);
        ArgumentNullException.ThrowIfNull(uiThreadDispatcher);
        ArgumentNullException.ThrowIfNull(modelScope);
        ArgumentNullException.ThrowIfNull(attachmentsViewModel);
        ArgumentNullException.ThrowIfNull(generationRunner);
        ArgumentNullException.ThrowIfNull(generationPanelStateService);
        ArgumentNullException.ThrowIfNull(imageViewerService);
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(errorHandler);

        _generationModelCatalogApiClient = generationModelCatalogApiClient;
        _imageModelOptionCatalog = imageModelOptionCatalog;
        _apiEndpointService = apiEndpointService;
        _uiThreadDispatcher = uiThreadDispatcher;
        AvailableModels = new ReadOnlyObservableCollection<ImageModelOption>(_availableModels);
        Quote = quote;
        _filePickerService = filePickerService;
        _secretStore = secretStore;
        _modelScope = modelScope;
        _attachmentsViewModel = attachmentsViewModel;
        _generationRunner = generationRunner;
        _generationPanelStateService = generationPanelStateService;
        _imageViewerService = imageViewerService;
        _errorHandler = errorHandler;
        _attachmentsViewModel.AttachmentStateChanged += OnAttachmentStateChanged;
        _apiEndpointService.BaseAddressChanged += OnApiBaseAddressChanged;
        ApplyCatalogSnapshot(imageModelOptionCatalog.GetModels());
    }

    public bool SupportsModel(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        return AvailableModels.Any(model =>
            string.Equals(model.Id, modelId, StringComparison.Ordinal)
            && _modelScope.SupportsModel(model));
    }

    public async Task PrepareStateRestoreAsync(CancellationToken ct)
    {
        try
        {
            if (LoadModelCatalogCommand.CanExecute(null))
            {
                await LoadModelCatalogCommand.ExecuteAsync(null);
            }
        }
        finally
        {
            _acceptApiEndpointChanges = true;
        }
    }

    public async Task RestoreStateAsync(CancellationToken ct)
    {
        if (RestorePanelStateCommand.CanExecute(null))
        {
            await RestorePanelStateCommand.ExecuteAsync(null);
        }
    }

    public async Task CommitPendingStateAsync(CancellationToken ct)
    {
        if (_promptStateSaveCancellation is null)
        {
            return;
        }

        CancelPendingPromptStateSave();
        await SavePanelStateAsync(nameof(CommitPendingStateAsync), ct);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _attachmentsViewModel.AttachmentStateChanged -= OnAttachmentStateChanged;
        _apiEndpointService.BaseAddressChanged -= OnApiBaseAddressChanged;
        _disposeCancellationSource.Cancel();
        CancelCatalogReload();
        CancelPendingPromptStateSave();
        _disposeCancellationSource.Dispose();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if ((e.PropertyName == nameof(IsLoading))
            || (e.PropertyName == nameof(IsCatalogLoading)))
        {
            NotifyCatalogStateChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadModelCatalog))]
    private async Task LoadModelCatalogAsync(CancellationToken ct)
    {
        await LoadModelCatalogCoreAsync(false, ct);
    }

    private async Task LoadModelCatalogCoreAsync(
        bool clearExistingCatalog,
        CancellationToken ct)
    {
        long endpointRevision = _apiEndpointService.Revision;
        long operationId = ++_catalogLoadOperationId;

        try
        {
            IsCatalogLoading = true;
            ErrorMessage = null;

            if (clearExistingCatalog)
            {
                _imageModelOptionCatalog.Clear();
                ApplyCatalogSnapshot([]);
            }

            GenerationModelCatalogDto catalog = await _generationModelCatalogApiClient.GetCatalogAsync(ct);

            if (!IsCurrentCatalogLoad(operationId, endpointRevision))
            {
                return;
            }

            _imageModelOptionCatalog.Initialize(catalog);
            ApplyCatalogSnapshot(_imageModelOptionCatalog.GetModels());
            await RestorePanelStateCoreAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (IsCurrentCatalogLoad(operationId, endpointRevision))
            {
                ErrorMessage = null;
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentCatalogLoad(operationId, endpointRevision))
            {
                _errorHandler.Log(ex, nameof(LoadModelCatalogAsync));
                ErrorMessage = UiStrings.ModelCatalogLoadFailed;
            }
        }
        finally
        {
            if (IsCurrentCatalogLoad(operationId, endpointRevision))
            {
                IsCatalogLoading = false;
                NotifyCatalogStateChanged();

                if (HasLoadedCatalog)
                {
                    RefreshPricePreview();
                }
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanPickImage))]
    private async Task PickImageAsync(CancellationToken ct)
    {
        try
        {
            ErrorMessage = null;
            IReadOnlyList<ImageAttachmentInput> inputs = await _filePickerService.PickImagesAsync(
                AttachmentInputByteLimit,
                ct);
            await AttachImageInputsCoreAsync(inputs, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(PickImageAsync));
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
        finally
        {
            RefreshAttachmentState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanAttachImages), AllowConcurrentExecutions = true)]
    private async Task AttachImagesAsync(
        IReadOnlyList<AttachedImageDto>? attachedImages,
        CancellationToken ct)
    {
        await ExecuteAttachmentOperationAsync(
            operationCt => AttachImagesCoreAsync(attachedImages, operationCt),
            nameof(AttachImagesAsync),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanAttachImageInputs), AllowConcurrentExecutions = true)]
    private async Task AttachImageInputsAsync(
        IReadOnlyList<ImageAttachmentInput>? inputs,
        CancellationToken ct)
    {
        await ExecuteAttachmentOperationAsync(
            operationCt => AttachImageInputsCoreAsync(inputs, operationCt),
            nameof(AttachImageInputsAsync),
            ct);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RemoveAttachmentAsync(
        AttachedImageViewModel? attachedImage,
        CancellationToken ct)
    {
        if (attachedImage is null)
        {
            return;
        }

        try
        {
            ErrorMessage = null;
            await _attachmentsViewModel.RemoveAttachmentAsync(PanelId, attachedImage, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(RemoveAttachmentAsync));
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
        finally
        {
            RefreshAttachmentState();
        }
    }

    [RelayCommand]
    private async Task ReorderAttachmentAsync(
        AttachedImageReorderRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return;
        }

        try
        {
            ErrorMessage = null;
            _attachmentsViewModel.MoveAttachment(request.AttachedImage, request.TargetIndex);
            await SavePanelStateAsync(nameof(ReorderAttachmentAsync), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(ReorderAttachmentAsync));
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenAttachmentAsync(
        AttachedImageViewModel? attachedImage,
        CancellationToken ct)
    {
        if (attachedImage is null)
        {
            return;
        }

        if (!attachedImage.IsReady)
        {
            return;
        }

        if (!AttachedImages.Contains(attachedImage))
        {
            return;
        }

        try
        {
            ErrorMessage = null;
            GalleryImageViewerRequest request = new(
                new GalleryDelegateImageViewerItemsSource(CreateAttachedImageViewerItems),
                attachedImage.Id,
                AttachImagesCommand);

            await _imageViewerService.OpenAsync(request, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(OpenAttachmentAsync));
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
    }

    private IReadOnlyList<GalleryImageViewerItem> CreateAttachedImageViewerItems()
    {
        return _attachmentsViewModel
            .GetReadyAttachedImages()
            .Select(CreateAttachedImageViewerItem)
            .ToList();
    }

    private static GalleryImageViewerItem CreateAttachedImageViewerItem(AttachedImageViewModel image)
    {
        return new GalleryImageViewerItem(
            image.Id,
            new GalleryAttachedImageViewerSource(image.ToDto()));
    }

    [RelayCommand]
    private async Task RestorePanelStateAsync(CancellationToken ct)
    {
        await RestorePanelStateCoreAsync(ct);
    }

    [RelayCommand]
    private async Task CommitPromptAsync(CancellationToken ct)
    {
        CancelPendingPromptStateSave();
        await SavePanelStateAsync(nameof(CommitPromptAsync), ct);
    }

    [RelayCommand]
    private void ResetTemperature()
    {
        if (SelectedModel is null)
        {
            return;
        }

        _hasTemperatureValue = true;
        Temperature = GenerationPanelOptionDefaults.GetDefaultTemperature(SelectedModel);
    }

    [RelayCommand]
    private void ResetThinkingLevel()
    {
        if (SelectedModel?.Thinking is null)
        {
            return;
        }

        SelectedThinkingLevel = ResolveThinkingLevelOption(
            SelectedModel.Thinking.Default,
            SelectedModel);
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand), AllowConcurrentExecutions = true)]
    private async Task GenerateAsync(CancellationToken ct)
    {
        try
        {
            ErrorMessage = null;
            await SavePanelStateAsync(nameof(GenerateAsync), ct);
            ImageModelOption selectedModel = SelectedModel
                ?? throw new InvalidOperationException(SelectedModelNotInitializedMessage);
            string providerCredential = string.Empty;

            if (RequiresProviderCredential(selectedModel))
            {
                string? storedCredential = await _secretStore
                    .GetSecretAsync(GoogleApiKeySettingDefinition.SecretNameValue, ct);

                if (string.IsNullOrWhiteSpace(storedCredential))
                {
                    ErrorMessage = UiStrings.GoogleApiKeyMissing;
                    return;
                }

                providerCredential = storedCredential.Trim();
            }

            await _generationRunner.RunAsync(
                CreateGenerationParameters(),
                providerCredential,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            HandleGenerationException(ex);
        }
    }

    private bool CanAttachImages(IReadOnlyList<AttachedImageDto>? attachedImages)
    {
        return !IsLoading && HasAttachmentCapacity();
    }

    private bool CanAttachImageInputs(IReadOnlyList<ImageAttachmentInput>? inputs)
    {
        return !IsLoading && HasAttachmentCapacity();
    }

    private async Task AttachImagesCoreAsync(
        IReadOnlyList<AttachedImageDto>? images,
        CancellationToken ct)
    {
        if (images?.Any(image => image is null) == true)
        {
            ErrorMessage = UiStrings.ImageAttachmentFailed;
            return;
        }

        IReadOnlyList<ImageAttachmentInput>? inputs = images?
            .Select(ImageAttachmentInput.FromImage)
            .ToList();
        await AttachImageInputsCoreAsync(inputs, ct);
    }

    private async Task AttachImageInputsCoreAsync(
        IReadOnlyList<ImageAttachmentInput>? inputs,
        CancellationToken ct)
    {
        if (SelectedModel is null)
        {
            ErrorMessage = UiStrings.ModelCatalogLoadFailed;
            DisposeInputs(inputs);
            return;
        }

        await _attachmentsViewModel.AttachInputsAsync(PanelId, SelectedModel, inputs, ct);
    }

    private async Task ExecuteAttachmentOperationAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken ct)
    {
        ErrorMessage = null;

        try
        {
            await operation(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, operationName);
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
        finally
        {
            RefreshAttachmentState();
        }
    }

    private NanoBanana2GenerationParameters CreateGenerationParameters()
    {
        ImageModelOption selectedModel = SelectedModel
            ?? throw new InvalidOperationException(SelectedModelNotInitializedMessage);

        return new NanoBanana2GenerationParameters(
            selectedModel,
            DisplayName,
            Prompt,
            SelectedAspectRatio,
            SelectedResolution,
            Temperature,
            GenerationCount,
            _attachmentsViewModel.GetAttachedImageDtos(),
            SelectedThinkingLevel?.Value);
    }

    private void RefreshAttachmentState()
    {
        OnPropertyChanged(nameof(AttachmentCounterText));
        PickImageCommand.NotifyCanExecuteChanged();
        AttachImagesCommand.NotifyCanExecuteChanged();
        AttachImageInputsCommand.NotifyCanExecuteChanged();
        RefreshPricePreview();
    }

    private void OnAttachmentStateChanged(object? sender, AttachmentStateChangedEventArgs e)
    {
        _ = sender;

        OnPropertyChanged(nameof(IsAttaching));
        GenerateCommand.NotifyCanExecuteChanged();
        RefreshAttachmentState();

        if (e.Kind is AttachmentStateChangeKind.Completed or AttachmentStateChangeKind.Removed)
        {
            SchedulePanelStateSave(nameof(OnAttachmentStateChanged));
        }

        if (e.Kind != AttachmentStateChangeKind.Failed)
        {
            return;
        }

        if (e.Exception is null)
        {
            ErrorMessage = UiStrings.ImageAttachmentFailed;
            return;
        }

        _errorHandler.Log(e.Exception, nameof(AttachImageInputsAsync));
        ErrorMessage = _errorHandler.GetUserMessage(e.Exception);
    }

    private void OnApiBaseAddressChanged(object? sender, EventArgs e)
    {
        if (!_acceptApiEndpointChanges || _isDisposed)
        {
            return;
        }

        _ = DispatchCatalogReloadAsync();
    }

    private void OnSelectedModelChanged(ImageModelOption? value)
    {
        if (value is null)
        {
            ResetSelectedModelValues();
            return;
        }

        string previousAspectRatio = SelectedAspectRatio;
        string previousResolution = SelectedResolution;
        double? previousTemperature = _hasTemperatureValue ? Temperature : null;
        string? previousThinkingLevel = SelectedThinkingLevel?.Value
            ?? _rememberedThinkingLevelValue;
        int previousGenerationCount = GenerationCount;

        NotifySelectedModelMetadataChanged();

        bool wasPanelStateSaveSuppressed = _suppressPanelStateSave;

        try
        {
            _suppressPricePreviewRefresh = true;
            _suppressPanelStateSave = true;
            ApplyCompatibleSelectionValues(
                value,
                previousAspectRatio,
                previousResolution,
                previousTemperature,
                previousThinkingLevel,
                previousGenerationCount);
        }
        finally
        {
            _suppressPanelStateSave = wasPanelStateSaveSuppressed;
            _suppressPricePreviewRefresh = false;
        }

        PickImageCommand.NotifyCanExecuteChanged();
        AttachImagesCommand.NotifyCanExecuteChanged();
        AttachImageInputsCommand.NotifyCanExecuteChanged();
        GenerateCommand.NotifyCanExecuteChanged();
        RefreshPricePreview();
        SchedulePanelStateSave(nameof(OnSelectedModelChanged));
    }

    partial void OnSelectedAspectRatioChanged(string value)
    {
        RefreshPricePreviewUnlessSuppressed();
        SchedulePanelStateSave(nameof(OnSelectedAspectRatioChanged));
    }

    partial void OnSelectedResolutionChanged(string value)
    {
        RefreshPricePreviewUnlessSuppressed();
        SchedulePanelStateSave(nameof(OnSelectedResolutionChanged));
    }

    partial void OnGenerationCountChanged(int value)
    {
        RefreshPricePreviewUnlessSuppressed();
        SchedulePanelStateSave(nameof(OnGenerationCountChanged));
    }

    partial void OnTemperatureChanged(double value)
    {
        OnPropertyChanged(nameof(TemperatureText));
        SchedulePanelStateSave(nameof(OnTemperatureChanged));
    }

    partial void OnSelectedThinkingLevelChanged(GenerationModelThinkingLevelMetadataDto? value)
    {
        if (value is not null)
        {
            _rememberedThinkingLevelValue = value.Value;
        }

        SchedulePanelStateSave(nameof(OnSelectedThinkingLevelChanged));
    }

    partial void OnPromptChanged(string value)
    {
        RefreshPricePreview();
        GenerateCommand.NotifyCanExecuteChanged();
        SchedulePromptStateSave();
    }

    private void RefreshPricePreview()
    {
        if (!CanRefreshPricePreview())
        {
            return;
        }

        try
        {
            Quote.Refresh(CreateGenerationParameters());
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(RefreshPricePreview));
        }
    }

    private void RefreshPricePreviewUnlessSuppressed()
    {
        if (_suppressPricePreviewRefresh)
        {
            return;
        }

        RefreshPricePreview();
    }

    private bool CanRefreshPricePreview()
    {
        return HasLoadedCatalog
            && !string.IsNullOrWhiteSpace(SelectedAspectRatio)
            && !string.IsNullOrWhiteSpace(SelectedResolution)
            && GenerationCount > 0;
    }

    private bool IsCurrentCatalogLoad(long operationId, long endpointRevision)
    {
        return operationId == _catalogLoadOperationId
            && endpointRevision == _apiEndpointService.Revision;
    }

    private async Task DispatchCatalogReloadAsync()
    {
        await ViewModelUiDispatch.RunAsync(
            _uiThreadDispatcher,
            ReloadCatalogAsync,
            _disposeCancellationSource.Token,
            _errorHandler,
            nameof(DispatchCatalogReloadAsync));
    }

    private async Task ReloadCatalogAsync()
    {
        LoadModelCatalogCommand.Cancel();
        CancelCatalogReload();

        CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _disposeCancellationSource.Token);
        _catalogReloadCancellation = cancellation;

        try
        {
            await LoadModelCatalogCoreAsync(true, cancellation.Token);
        }
        finally
        {
            if (ReferenceEquals(_catalogReloadCancellation, cancellation))
            {
                _catalogReloadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelCatalogReload()
    {
        _catalogReloadCancellation?.Cancel();
    }

    private bool HasAttachmentCapacity()
    {
        return SelectedModel is not null && AttachedImages.Count < SelectedModel.MaxAttachedImages;
    }

    private static bool RequiresProviderCredential(ImageModelOption selectedModel)
    {
        return string.Equals(selectedModel.Provider, GenerationProviderIds.Google, StringComparison.Ordinal);
    }

    private void HandleGenerationException(Exception exception)
    {
        _errorHandler.Log(exception, nameof(GenerateAsync));
        ErrorMessage = _errorHandler.GetUserMessage(exception);
    }

    private void ApplyCatalogSnapshot(IReadOnlyList<ImageModelOption> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        _availableModels.Clear();

        IReadOnlyList<ImageModelOption> supportedModels = models
            .Where(_modelScope.SupportsModel)
            .ToList();

        foreach (ImageModelOption model in supportedModels)
        {
            _availableModels.Add(model);
        }

        bool wasPanelStateSaveSuppressed = _suppressPanelStateSave;
        bool wasSelectionValueResetNotificationsSuppressed = _suppressSelectionValueResetNotifications;
        _suppressPanelStateSave = true;
        _suppressSelectionValueResetNotifications = true;

        try
        {
            SelectedModel = supportedModels.Count > 0
                ? GenerationPanelOptionDefaults.GetDefaultModel(supportedModels)
                : null;
        }
        finally
        {
            _suppressPanelStateSave = wasPanelStateSaveSuppressed;
            _suppressSelectionValueResetNotifications = wasSelectionValueResetNotificationsSuppressed;
        }

        if (SelectedModel is not null)
        {
            ErrorMessage = null;
        }

        NotifyCatalogStateChanged();
    }

    private async Task RestorePanelStateCoreAsync(CancellationToken ct)
    {
        if (_hasRestoredPanelState || !CanRestorePanelState())
        {
            return;
        }

        try
        {
            GenerationPanelState state = await _generationPanelStateService.LoadAsync(PanelId, ct);
            await ApplyPanelStateAsync(state, ct);
            _hasRestoredPanelState = true;
            RestorePanelStateCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(RestorePanelStateCoreAsync));
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
    }

    private bool CanRestorePanelState()
    {
        return _imageModelOptionCatalog.IsLoaded && SelectedModel is not null;
    }

    private async Task ApplyPanelStateAsync(GenerationPanelState state, CancellationToken ct)
    {
        ImageModelOption? selectedModel = AvailableModels.FirstOrDefault(model =>
            string.Equals(model.Id, state.SelectedModelId, StringComparison.Ordinal));

        if (selectedModel is null)
        {
            return;
        }

        bool wasPanelStateSaveSuppressed = _suppressPanelStateSave;
        bool wasPricePreviewRefreshSuppressed = _suppressPricePreviewRefresh;
        bool wasSelectionValueResetNotificationsSuppressed = _suppressSelectionValueResetNotifications;
        _suppressPanelStateSave = true;
        _suppressPricePreviewRefresh = true;
        _suppressSelectionValueResetNotifications = true;

        try
        {
            _rememberedThinkingLevelValue = state.ThinkingLevel;
            SelectedModel = selectedModel;
            SelectedAspectRatio = GenerationPanelOptionCompatibility.ResolveString(
                state.AspectRatio,
                selectedModel.AspectRatios,
                GenerationPanelOptionDefaults.GetDefaultAspectRatio(selectedModel))
                .Value;
            SelectedResolution = GenerationPanelOptionCompatibility.ResolveString(
                state.Resolution,
                selectedModel.Resolutions,
                GenerationPanelOptionDefaults.GetDefaultResolution(selectedModel))
                .Value;
            Temperature = GenerationPanelOptionCompatibility.ResolveTemperature(
                state.Temperature,
                selectedModel.Temperature)
                .Value;
            _hasTemperatureValue = true;
            SelectedThinkingLevel = ResolveThinkingLevelOption(
                state.ThinkingLevel,
                selectedModel);
            GenerationCount = GenerationPanelOptionCompatibility.ResolveGenerationCount(
                state.GenerationCount,
                selectedModel)
                .Value;
            Prompt = state.Prompt;
            await _attachmentsViewModel.RestoreAsync(PanelId, selectedModel, state.Attachments, ct);
        }
        finally
        {
            _suppressPanelStateSave = wasPanelStateSaveSuppressed;
            _suppressPricePreviewRefresh = wasPricePreviewRefreshSuppressed;
            _suppressSelectionValueResetNotifications = wasSelectionValueResetNotificationsSuppressed;
        }

        RefreshAttachmentState();
        RefreshPricePreview();
    }

    private void SchedulePanelStateSave(string operationName)
    {
        if (_suppressPanelStateSave || !HasLoadedCatalog)
        {
            return;
        }

        _ = SavePanelStateAsync(operationName, CancellationToken.None);
    }

    private void SchedulePromptStateSave()
    {
        if (_suppressPanelStateSave || !HasLoadedCatalog)
        {
            return;
        }

        CancelPendingPromptStateSave();

        CancellationTokenSource cancellation = new();
        _promptStateSaveCancellation = cancellation;
        _ = SavePromptStateAfterDelayAsync(cancellation);
    }

    private async Task SavePromptStateAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(PromptStateSaveDelay, cancellation.Token);

            if (!ReferenceEquals(_promptStateSaveCancellation, cancellation))
            {
                return;
            }

            await SavePanelStateAsync(nameof(SavePromptStateAfterDelayAsync), cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_promptStateSaveCancellation, cancellation))
            {
                _promptStateSaveCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelPendingPromptStateSave()
    {
        CancellationTokenSource? cancellation = _promptStateSaveCancellation;

        if (cancellation is null)
        {
            return;
        }

        _promptStateSaveCancellation = null;
        cancellation.Cancel();
    }

    private async Task SavePanelStateAsync(string operationName, CancellationToken ct)
    {
        if (_suppressPanelStateSave || !HasLoadedCatalog)
        {
            return;
        }

        try
        {
            await _generationPanelStateService.SaveAsync(
                PanelId,
                CreatePanelStateSnapshot(),
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, operationName);
        }
    }

    private GenerationPanelState CreatePanelStateSnapshot()
    {
        return new GenerationPanelState
        {
            PanelId = PanelId,
            SelectedModelId = SelectedModel?.Id ?? string.Empty,
            AspectRatio = SelectedAspectRatio,
            Resolution = SelectedResolution,
            Temperature = Temperature,
            ThinkingLevel = SelectedThinkingLevel?.Value
                ?? _rememberedThinkingLevelValue,
            GenerationCount = GenerationCount,
            Prompt = Prompt,
            Attachments = _attachmentsViewModel.GetAttachmentStates()
        };
    }

    private void ApplyCompatibleSelectionValues(
        ImageModelOption selectedModel,
        string previousAspectRatio,
        string previousResolution,
        double? previousTemperature,
        string? previousThinkingLevel,
        int previousGenerationCount)
    {
        (string selectedAspectRatio, bool aspectRatioWasReset) =
            GenerationPanelOptionCompatibility.ResolveString(
                previousAspectRatio,
                selectedModel.AspectRatios,
                GenerationPanelOptionDefaults.GetDefaultAspectRatio(selectedModel));
        (string selectedResolution, bool resolutionWasReset) =
            GenerationPanelOptionCompatibility.ResolveString(
                previousResolution,
                selectedModel.Resolutions,
                GenerationPanelOptionDefaults.GetDefaultResolution(selectedModel));
        double temperature = GenerationPanelOptionCompatibility.ResolveTemperature(
                previousTemperature,
                selectedModel.Temperature)
            .Value;
        GenerationModelThinkingLevelMetadataDto? thinkingLevel = ResolveThinkingLevelOption(
            previousThinkingLevel,
            selectedModel);
        (int generationCount, bool generationCountWasReset) =
            GenerationPanelOptionCompatibility.ResolveGenerationCount(
                previousGenerationCount,
                selectedModel);

        SelectedAspectRatio = selectedAspectRatio;
        SelectedResolution = selectedResolution;
        Temperature = temperature;
        _hasTemperatureValue = true;
        SelectedThinkingLevel = thinkingLevel;
        GenerationCount = generationCount;

        NotifySelectionValueReset(aspectRatioWasReset, resolutionWasReset, generationCountWasReset);
    }

    private void NotifySelectionValueReset(
        bool aspectRatioWasReset,
        bool resolutionWasReset,
        bool generationCountWasReset)
    {
        if (_suppressSelectionValueResetNotifications)
        {
            return;
        }

        if (aspectRatioWasReset)
        {
            SelectionValueReset?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(SelectedAspectRatio)));
        }

        if (resolutionWasReset)
        {
            SelectionValueReset?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(SelectedResolution)));
        }

        if (generationCountWasReset)
        {
            SelectionValueReset?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(GenerationCount)));
        }
    }

    private void ResetSelectedModelValues()
    {
        try
        {
            _suppressPricePreviewRefresh = true;
            SelectedAspectRatio = string.Empty;
            SelectedResolution = string.Empty;
            _hasTemperatureValue = false;
            Temperature = 0d;
            _rememberedThinkingLevelValue = null;
            SelectedThinkingLevel = null;
            GenerationCount = 0;
        }
        finally
        {
            _suppressPricePreviewRefresh = false;
        }

        NotifySelectedModelMetadataChanged();
        NotifyCatalogStateChanged();
    }

    private static GenerationModelThinkingLevelMetadataDto? ResolveThinkingLevelOption(
        string? value,
        ImageModelOption selectedModel)
    {
        string? resolvedValue = GenerationPanelOptionCompatibility.ResolveThinkingLevel(
                value,
                selectedModel.Thinking)
            .Value;

        if (resolvedValue is null || selectedModel.Thinking is null)
        {
            return null;
        }

        return selectedModel.Thinking.Levels.Single(level =>
            string.Equals(level.Value, resolvedValue, StringComparison.Ordinal));
    }

    private void NotifyCatalogStateChanged()
    {
        OnPropertyChanged(nameof(HasLoadedCatalog));
        LoadModelCatalogCommand.NotifyCanExecuteChanged();
        GenerateCommand.NotifyCanExecuteChanged();
        PickImageCommand.NotifyCanExecuteChanged();
        AttachImagesCommand.NotifyCanExecuteChanged();
        AttachImageInputsCommand.NotifyCanExecuteChanged();
    }

    private void NotifySelectedModelMetadataChanged()
    {
        OnPropertyChanged(nameof(ModelId));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(AspectRatios));
        OnPropertyChanged(nameof(Resolutions));
        OnPropertyChanged(nameof(GenerationCounts));
        OnPropertyChanged(nameof(MinimumTemperature));
        OnPropertyChanged(nameof(MaximumTemperature));
        OnPropertyChanged(nameof(DefaultTemperature));
        OnPropertyChanged(nameof(TemperatureStep));
        OnPropertyChanged(nameof(ThinkingLevels));
        OnPropertyChanged(nameof(SupportsThinkingLevel));
        OnPropertyChanged(nameof(AttachmentCounterText));
        OnPropertyChanged(nameof(MaxAttachedImageBytes));
        OnPropertyChanged(nameof(AttachmentInputByteLimit));
    }

    private void DisposeInputs(IEnumerable<ImageAttachmentInput>? inputs)
    {
        if (inputs is null)
        {
            return;
        }

        foreach (ImageAttachmentInput input in inputs)
        {
            input.Dispose();
        }
    }
}
