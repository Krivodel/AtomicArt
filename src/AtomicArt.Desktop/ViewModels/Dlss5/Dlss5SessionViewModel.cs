using System.Diagnostics;

using Microsoft.Extensions.Logging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.ViewModels.Dlss5;

public sealed partial class Dlss5SessionViewModel : ObservableObject, IDlss5SourceOpener
{
    public int StyleIndex
    {
        get => (int)Style;
        set => Style = (Dlss5Style)Math.Clamp(value, (int)Dlss5Style.Default, (int)Dlss5Style.Cinematic);
    }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasSource => SourceImage is not null;
    public bool CanInteractWithImages => HasSource && !IsSourceLoading && !IsRendering;
    public object? ResultPreviewImage => ResultImage ?? SourceImage;

    private const int SliderPrewarmCapacity = 1;
    private const int SliderPrewarmDelayMilliseconds = 90;
    private const int PngQuality = 100;

    private static readonly SKPngEncoderOptions SourcePngEncoderOptions =
        new(SKPngEncoderFilterFlags.NoFilters, 0);

    private readonly IFilePickerService _filePickerService;
    private readonly IClipboardImageService _clipboardImageService;
    private readonly IDialogService _dialogService;
    private readonly IDlss5ModuleInstaller _moduleInstaller;
    private readonly IDlss5NativeEngine _nativeEngine;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILogger<Dlss5SessionViewModel> _logger;
    private readonly IDlss5RenderScheduler _renderScheduler;
    private readonly Dlss5ModulePaths _paths;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly IAppStateStore _stateStore;
    private readonly IStateWriteScheduler _stateWriteScheduler;
    private readonly Dlss5SessionStateSection _stateSection;
    private readonly IImageViewerService _imageViewerService;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly IDlss5DisplayImageFactory _displayImageFactory;
    private readonly Dlss5RenderBitmapCache _renderCache = new();
    private readonly object _sliderPrewarmSync = new();
    private readonly LinkedList<CancellationTokenSource> _sliderPrewarmCancellations = [];
    private CancellationTokenSource? _sliderPrewarmDebounceCancellation;
    private SKBitmap? _sourceBitmap;
    private SKBitmap? _resultBitmap;
    private IDlss5DisplayImage? _sourceDisplayImage;
    private IDlss5DisplayImage? _resultDisplayImage;
    private string? _sourceFileName;
    private long _renderRevision;
    private long _sourceLoadRevision;
    private bool _isRestoringState;
    private bool _hasStartedWorker;
    private long _sourceGeneration;
    private CancellationTokenSource? _prewarmCancellation;
    private TaskCompletionSource<bool>? _installNotificationCompletion;
    [ObservableProperty]
    private bool _isOpen;
    [ObservableProperty]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    [ObservableProperty]
    private int _operationProgress;
    [ObservableProperty]
    private string _operationLocalizationKey = Dlss5LocalizationKeys.Downloading;
    [ObservableProperty]
    private bool _isOperationProgressIndeterminate;
    [ObservableProperty]
    private Dlss5Style _style = Dlss5RenderSettings.Default.Style;
    [ObservableProperty]
    private float _generalIntensity = Dlss5RenderSettings.Default.GeneralIntensity;
    [ObservableProperty]
    private float _localStructureIntensity = Dlss5RenderSettings.Default.LocalStructureIntensity;
    [ObservableProperty]
    private float _skinStructureStrength = Dlss5RenderSettings.Default.SkinStructureStrength;
    [ObservableProperty]
    private float _localToneStrength = Dlss5RenderSettings.Default.LocalToneStrength;
    [ObservableProperty]
    private double _parameterAreaHeight = Dlss5SessionState.DefaultParameterAreaHeight;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSource))]
    [NotifyPropertyChangedFor(nameof(CanInteractWithImages))]
    [NotifyPropertyChangedFor(nameof(ResultPreviewImage))]
    private object? _sourceImage;
    [ObservableProperty]
    private int _sourceWidth;
    [ObservableProperty]
    private int _sourceHeight;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteractWithImages))]
    private bool _isSourceLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultPreviewImage))]
    private object? _resultImage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteractWithImages))]
    private bool _isRendering;
    [ObservableProperty]
    private bool _isStartingWorker;
    [ObservableProperty]
    private bool _isInstallCompleted;

    public Dlss5SessionViewModel(
        IFilePickerService filePickerService,
        IClipboardImageService clipboardImageService,
        IDialogService dialogService,
        IDlss5ModuleInstaller moduleInstaller,
        IDlss5NativeEngine nativeEngine,
        IDlss5RenderScheduler renderScheduler,
        Dlss5ModulePaths paths,
        IUiThreadDispatcher uiThreadDispatcher,
        IAppStateStore stateStore,
        IStateWriteScheduler stateWriteScheduler,
        Dlss5SessionStateSection stateSection,
        IImageViewerService imageViewerService,
        IDataRootAccessCoordinator accessCoordinator,
        IDlss5DisplayImageFactory displayImageFactory,
        IViewModelErrorHandler errorHandler,
        ILogger<Dlss5SessionViewModel> logger)
    {
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _clipboardImageService = clipboardImageService ?? throw new ArgumentNullException(nameof(clipboardImageService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _moduleInstaller = moduleInstaller ?? throw new ArgumentNullException(nameof(moduleInstaller));
        _nativeEngine = nativeEngine ?? throw new ArgumentNullException(nameof(nativeEngine));
        _renderScheduler = renderScheduler ?? throw new ArgumentNullException(nameof(renderScheduler));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _uiThreadDispatcher = uiThreadDispatcher ?? throw new ArgumentNullException(nameof(uiThreadDispatcher));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _stateWriteScheduler = stateWriteScheduler ?? throw new ArgumentNullException(nameof(stateWriteScheduler));
        _stateSection = stateSection ?? throw new ArgumentNullException(nameof(stateSection));
        _imageViewerService = imageViewerService ?? throw new ArgumentNullException(nameof(imageViewerService));
        _accessCoordinator = accessCoordinator ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _displayImageFactory = displayImageFactory ?? throw new ArgumentNullException(nameof(displayImageFactory));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> OpenFromImagePathAsync(string imagePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        string sourcePath = Path.GetFullPath(imagePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected DLSS 5 source image no longer exists.", sourcePath);
        }

        long loadRevision = ++_sourceLoadRevision;
        IsSourceLoading = true;
        SKBitmap? bitmap = null;
        try
        {
            bitmap = await Dlss5SessionViewModel.DecodeBitmapAsync(sourcePath, ct)
                ?? throw new InvalidDataException("The selected image cannot be decoded for DLSS 5.");
            if ((loadRevision != _sourceLoadRevision) || (!await OpenRuntimeAsync(false, ct)))
            {
                return false;
            }

            if (loadRevision != _sourceLoadRevision)
            {
                return false;
            }

            (string temporaryPath, string destinationPath) = await Task.Run(
                () => CopySourceToSession(sourcePath, loadRevision), ct);
            try
            {
                if (loadRevision != _sourceLoadRevision)
                {
                    return false;
                }

                PublishSessionSource(temporaryPath, destinationPath);
                SKBitmap source = bitmap;
                bitmap = null;
                await ReplaceSourceAsync(source, Path.GetFileName(destinationPath), ct, loadRevision);
                return true;
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }
        finally
        {
            bitmap?.Dispose();
            if (loadRevision == _sourceLoadRevision)
            {
                IsSourceLoading = false;
            }
        }
    }

    public async Task RestoreAsync(CancellationToken ct)
    {
        Dlss5SessionState state = await _stateStore.LoadAsync<Dlss5SessionState>(_stateSection, ct);
        _isRestoringState = true;

        try
        {
            ApplyState(state);

            if (string.IsNullOrWhiteSpace(state.SourceFileName))
            {
                return;
            }

            string sourceFileName = Path.GetFileName(state.SourceFileName);
            string sourcePath = Path.Combine(_paths.ModuleDirectory, "session", sourceFileName);

            if (File.Exists(sourcePath))
            {
                _sourceFileName = sourceFileName;
            }
        }
        finally
        {
            _isRestoringState = false;
        }
    }

    public async Task<bool> OpenAsync(CancellationToken ct)
    {
        bool hasLoadedSource = _sourceBitmap is not null;
        if (!await OpenRuntimeAsync(hasLoadedSource, ct))
        {
            return false;
        }

        if (!hasLoadedSource)
        {
            await LoadPersistedSourceAsync(ct);
        }

        return true;
    }

    public void PrepareForDataRootMigration()
    {
        Close();
    }

    internal void CompleteInstallNotification(bool launch)
    {
        _installNotificationCompletion?.TrySetResult(launch);
    }

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKPixmap? pixmap = image.PeekPixels();
        SKData data = pixmap is not null
            ? pixmap.Encode(Dlss5SessionViewModel.SourcePngEncoderOptions)
                ?? throw new InvalidDataException("The DLSS 5 comparison image could not be encoded as PNG.")
            : image.Encode(SKEncodedImageFormat.Png, PngQuality)
                ?? throw new InvalidDataException("The DLSS 5 comparison image could not be encoded as PNG.");
        using (data)
        {
            return data.ToArray();
        }
    }

    private static Task<SKBitmap?> DecodeBitmapAsync(string imagePath, CancellationToken ct)
    {
        return Task.Run<SKBitmap?>(
            () =>
            {
                using FileStream stream = new(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return SKBitmap.Decode(stream);
            },
            ct);
    }

    private async Task<bool> OpenRuntimeAsync(bool queueExistingSource, CancellationToken ct)
    {
        ErrorMessage = null;
        using DataRootAccessLease accessLease = await _accessCoordinator
            .AcquireAccessAsync(ct);
        bool requiresInstallation = !_moduleInstaller.IsInstalled;

        if (requiresInstallation)
        {
            bool confirmed = await _dialogService.ShowConfirmationAsync(
                new LocalizedConfirmationDialogRequest(
                    Dlss5LocalizationKeys.Install.Title,
                    Dlss5LocalizationKeys.Install.Message,
                    Dlss5LocalizationKeys.Install.Confirm,
                    CommonLocalizationKeys.Cancel,
                    ConfirmationDialogKind.Standard,
                    ConfirmationDialogBackgroundClickBehavior.Ignore,
                    new object?[] { Dlss5FeatureDefinition.DownloadSizeDisplay }),
                ct);

            if (!confirmed)
            {
                return false;
            }
        }

        try
        {
            IsLoading = true;
            OperationProgress = 0;
            OperationLocalizationKey = Dlss5LocalizationKeys.Downloading;
            IsOperationProgressIndeterminate = false;
            Progress<Dlss5ModuleInstallProgress> progress = new(value =>
                OperationProgress = value.Percent);
            await _moduleInstaller.EnsureInstalledAsync(progress, ct);

            if (requiresInstallation)
            {
                IsLoading = false;
                IsOperationProgressIndeterminate = false;
                bool launch = await WaitForInstallNotificationAsync(ct);

                if (!launch)
                {
                    return false;
                }

                IsLoading = true;
            }

            OperationLocalizationKey = Dlss5LocalizationKeys.Starting;
            IsOperationProgressIndeterminate = true;
            await _nativeEngine.InitializeAsync(new Progress<int>(value => OperationProgress = value), ct);
            IsOpen = true;
            if (queueExistingSource)
            {
                QueueRender();
            }
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            _errorHandler.Log(exception, nameof(OpenAsync));
            ErrorMessage = _errorHandler.GetUserMessage(exception);
            _dialogService.ShowLocalizedError(Dlss5LocalizationKeys.Errors.InstallFailed);
            return false;
        }
        finally
        {
            IsLoading = false;
            IsOperationProgressIndeterminate = false;
        }
    }

    private async Task<bool> WaitForInstallNotificationAsync(CancellationToken ct)
    {
        if (_installNotificationCompletion is not null)
        {
            throw new InvalidOperationException("The DLSS 5 installation notification is already open.");
        }

        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _installNotificationCompletion = completion;
        IsInstallCompleted = true;

        try
        {
            return await completion.Task.WaitAsync(ct);
        }
        finally
        {
            if (ReferenceEquals(_installNotificationCompletion, completion))
            {
                _installNotificationCompletion = null;
            }

            IsInstallCompleted = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        IsOpen = false;
    }

    [RelayCommand]
    private void Close()
    {
        CancelImageOperations();
        _nativeEngine.Stop();
        _hasStartedWorker = false;
        ReleaseImageResources();
        IsOpen = false;
    }

    [RelayCommand]
    private void CommitSliderRender()
    {
        // Do not start a worker while the thumb is moving: setup takes several
        // seconds and an intermediate value would occupy the worker slot until
        // the user releases the slider. Start exactly one candidate for the
        // committed value, then let RenderAsync await and reuse it.
        CancelSliderWorkerPrewarmDebounce();
        QueueSliderWorkerPrewarm();
        QueueRender();
    }

    [RelayCommand]
    private void ClearSource()
    {
        CancelImageOperations();
        ReleaseImageResources();
        _sourceFileName = null;
        DeleteSessionSources(null);
        SaveState();
    }

    private void CancelImageOperations()
    {
        ++_sourceLoadRevision;
        IsSourceLoading = false;
        Interlocked.Increment(ref _renderRevision);
        CancelPrewarm();
        CancelSliderWorkerPrewarm();
        _renderScheduler.Cancel();
        IsRendering = false;
        IsStartingWorker = false;
        IsOperationProgressIndeterminate = false;
    }

    [RelayCommand]
    private async Task PickSourceAsync(CancellationToken ct)
    {
        try
        {
            IReadOnlyList<ImageAttachmentInput> inputs = await _filePickerService.PickImagesAsync(
                Dlss5FeatureDefinition.MaxSourceImageBytes, ct);
            await AttachSourceImagesAsync(inputs, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ReportSourceFailure(exception, nameof(PickSourceAsync));
        }
    }

    [RelayCommand]
    private async Task PasteSourceAsync(CancellationToken ct)
    {
        long loadRevision = ++_sourceLoadRevision;
        IsSourceLoading = true;
        try
        {
            ImageAttachmentInput? input = await _clipboardImageService.TryGetImageAsync(
                Dlss5FeatureDefinition.MaxSourceImageBytes, ct);
            await LoadSourceInputAsync(input, loadRevision, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ReportSourceFailure(exception, nameof(PasteSourceAsync));
        }
        finally
        {
            if (loadRevision == _sourceLoadRevision)
            {
                IsSourceLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task AttachSourceImagesAsync(IReadOnlyList<ImageAttachmentInput>? inputs, CancellationToken ct)
    {
        if (inputs is null)
        {
            return;
        }

        try
        {
            await PasteClipboardImageAsync(inputs.FirstOrDefault(), ct);
        }
        finally
        {
            for (int index = 1; index < inputs.Count; index++)
            {
                inputs[index].Dispose();
            }
        }
    }

    private void ReportSourceFailure(Exception exception, string operationName)
    {
        _errorHandler.Log(exception, operationName);
        ErrorMessage = _errorHandler.GetUserMessage(exception);
        _dialogService.ShowLocalizedError(Dlss5LocalizationKeys.Errors.SourceFailed);
    }

    [RelayCommand]
    private Task PasteClipboardImageAsync(ImageAttachmentInput? input, CancellationToken ct)
    {
        if (input is null)
        {
            return Task.CompletedTask;
        }

        return LoadSourceInputAsync(input, ++_sourceLoadRevision, ct);
    }

    private async Task LoadSourceInputAsync(ImageAttachmentInput? input, long loadRevision, CancellationToken ct)
    {
        using (input)
        {
            if ((input is null) || (loadRevision != _sourceLoadRevision))
            {
                return;
            }

            IsSourceLoading = true;
            try
            {
                AttachedImageDto? image = await input.ReadAsync(ct);
                if ((image is null) || (loadRevision != _sourceLoadRevision))
                {
                    return;
                }

                string sessionDirectory = Path.Combine(_paths.ModuleDirectory, "session");
                Directory.CreateDirectory(sessionDirectory);
                string sourceFileName = "source.png";
                string destinationPath = Path.Combine(sessionDirectory, sourceFileName);
                string temporaryPath = $"{destinationPath}.{loadRevision}.partial";
                SKBitmap? bitmap = await Task.Run(() => SKBitmap.Decode(image.Content), ct)
                    ?? throw new InvalidDataException("The selected image cannot be decoded for DLSS 5.");

                try
                {
                    byte[] encodedSource = await Task.Run(() => Dlss5SessionViewModel.EncodePng(bitmap), ct);
                    await File.WriteAllBytesAsync(temporaryPath, encodedSource, ct);
                    if (loadRevision != _sourceLoadRevision)
                    {
                        return;
                    }

                    PublishSessionSource(temporaryPath, destinationPath);
                    SKBitmap source = bitmap;
                    bitmap = null;
                    await ReplaceSourceAsync(source, sourceFileName, ct, loadRevision);
                }
                finally
                {
                    bitmap?.Dispose();
                    File.Delete(temporaryPath);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                ReportSourceFailure(exception, nameof(PasteClipboardImageAsync));
            }
            finally
            {
                if (loadRevision == _sourceLoadRevision)
                {
                    IsSourceLoading = false;
                }
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenComparison))]
    private async Task OpenComparisonAsync(string? selectedImage, CancellationToken ct)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        if ((_sourceFileName is null)
            || (_sourceBitmap is null)
            || (_sourceDisplayImage is null)
            || (_resultBitmap is null)
            || (_resultDisplayImage is null))
        {
            _logger.LogWarning(
                "DLSS 5 comparison click ignored because sourceNameReady={SourceNameReady}, sourceBitmapReady={SourceBitmapReady}, resultReady={ResultReady}.",
                _sourceFileName is not null,
                _sourceBitmap is not null,
                _resultBitmap is not null);
            return;
        }

        string sourceFileName = Path.GetFileName(_sourceFileName);
        if ((string.IsNullOrWhiteSpace(sourceFileName))
            || (!string.Equals(sourceFileName, _sourceFileName, StringComparison.Ordinal)))
        {
            _logger.LogWarning(
                "DLSS 5 comparison click ignored because the persisted source file name is invalid.");
            return;
        }

        string sourcePath = Path.Combine(_paths.ModuleDirectory, "session", sourceFileName);

        bool sourceFileExists = File.Exists(sourcePath);
        _logger.LogInformation(
            "DLSS 5 comparison requested for source {SourceFileName}; sourceFileExists={SourceFileExists}; using in-process bitmaps.",
            sourceFileName,
            sourceFileExists);

        Guid sourceId = Guid.NewGuid();
        Guid resultId = Guid.NewGuid();
        List<GalleryImageViewerItem> items =
        [
            new GalleryImageViewerItem(
                sourceId,
                new GalleryBitmapImageViewerSource(
                    "dlss5",
                    sourceFileName,
                    new Dlss5DisplayImageBitmapSource(
                        _sourceDisplayImage,
                        sourceFileExists),
                    sourceFileExists ? sourcePath : null)),
            new GalleryImageViewerItem(
                resultId,
                new GalleryBitmapImageViewerSource(
                    "dlss5",
                    "dlss5-result.png",
                    new Dlss5DisplayImageBitmapSource(
                        _resultDisplayImage,
                        isFileBacked: false)))
        ];

        await _imageViewerService.OpenAsync(
            new GalleryImageViewerRequest(
                new GalleryStaticImageViewerItemsSource(items),
                string.Equals(selectedImage, "result", StringComparison.Ordinal) ? resultId : sourceId,
                null),
            ct);
        _logger.LogInformation(
            "DLSS 5 comparison viewer opened in {ElapsedMilliseconds} ms using in-process bitmaps.",
            stopwatch.ElapsedMilliseconds);
    }

    private async Task ReplaceSourceAsync(
        SKBitmap bitmap,
        string sourceFileName,
        CancellationToken ct,
        long loadRevision)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        IDlss5DisplayImage sourceImage;
        try
        {
            CancelSliderWorkerPrewarm();
            sourceImage = await Task.Run(
                    () => _displayImageFactory.Create(bitmap),
                    ct);
        }
        catch (Exception)
        {
            bitmap.Dispose();
            throw;
        }

        try
        {
            await _uiThreadDispatcher.InvokeAsync(
                () =>
                {
                    if (loadRevision != _sourceLoadRevision)
                    {
                        sourceImage.Dispose();
                        bitmap.Dispose();
                        return;
                    }

                    ReplaceSource(bitmap, sourceFileName, sourceImage);
                },
                ct);
        }
        catch (Exception)
        {
            sourceImage.Dispose();
            bitmap.Dispose();
            throw;
        }
    }

    private async Task LoadPersistedSourceAsync(CancellationToken ct)
    {
        string? sourceFileName = _sourceFileName;
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            return;
        }

        string sourcePath = Path.Combine(_paths.ModuleDirectory, "session", sourceFileName);
        if (!File.Exists(sourcePath))
        {
            return;
        }

        long loadRevision = ++_sourceLoadRevision;
        IsSourceLoading = true;
        try
        {
            SKBitmap? bitmap = await Dlss5SessionViewModel.DecodeBitmapAsync(sourcePath, ct);
            if (bitmap is not null)
            {
                DeleteSessionSources(sourcePath);
                await ReplaceSourceAsync(bitmap, sourceFileName, ct, loadRevision);
            }
        }
        finally
        {
            if (loadRevision == _sourceLoadRevision)
            {
                IsSourceLoading = false;
            }
        }
    }

    private void ReplaceSource(
        SKBitmap bitmap,
        string sourceFileName,
        IDlss5DisplayImage sourceImage)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(sourceImage);
        CancelPrewarm();
        _sourceBitmap?.Dispose();
        _sourceBitmap = bitmap;
        Interlocked.Increment(ref _sourceGeneration);
        ClearRenderCache();
        _sourceFileName = sourceFileName;
        _sourceDisplayImage?.Dispose();
        _sourceDisplayImage = sourceImage;
        SourceWidth = bitmap.Width;
        SourceHeight = bitmap.Height;
        SourceImage = sourceImage.Value;
        _resultDisplayImage?.Dispose();
        _resultDisplayImage = null;
        ResultImage = null;
        _resultBitmap?.Dispose();
        _resultBitmap = null;
        OpenComparisonCommand.NotifyCanExecuteChanged();
        SaveAndQueueRender();
    }

    private (string TemporaryPath, string DestinationPath) CopySourceToSession(string sourcePath, long loadRevision)
    {
        string sessionDirectory = Path.Combine(_paths.ModuleDirectory, "session");
        Directory.CreateDirectory(sessionDirectory);
        string extension = Path.GetExtension(sourcePath);
        string destinationPath = Path.Combine(sessionDirectory, $"source{extension}");
        string temporaryPath = $"{destinationPath}.{loadRevision}.partial";
        File.Copy(sourcePath, temporaryPath, overwrite: true);
        return (temporaryPath, destinationPath);
    }

    private void PublishSessionSource(string temporaryPath, string destinationPath)
    {
        File.Move(temporaryPath, destinationPath, overwrite: true);
        DeleteSessionSources(destinationPath);
    }

    private void DeleteSessionSources(string? preservedSourcePath)
    {
        string sessionDirectory = Path.Combine(_paths.ModuleDirectory, "session");
        if (!Directory.Exists(sessionDirectory))
        {
            return;
        }

        string[] sourcePaths;
        try
        {
            sourcePaths = Directory.GetFiles(
                sessionDirectory,
                "source.*",
                SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                exception,
                "Unable to enumerate stale DLSS 5 session sources in {SessionDirectory}.",
                sessionDirectory);
            return;
        }

        foreach (string sourcePath in sourcePaths)
        {
            if ((preservedSourcePath is not null)
                && string.Equals(sourcePath, preservedSourcePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sourcePath.EndsWith(".partial", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(sourcePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to remove stale DLSS 5 session source {SourcePath}.",
                    sourcePath);
            }
        }
    }

    private void QueueRender()
    {
        if ((!IsOpen) || (_sourceBitmap is null) || (!_nativeEngine.IsInitialized))
        {
            return;
        }

        ErrorMessage = null;
        if (!_hasStartedWorker)
        {
            IsStartingWorker = true;
            OperationLocalizationKey = Dlss5LocalizationKeys.Starting;
            OperationProgress = 0;
            IsOperationProgressIndeterminate = true;
        }

        long revision = Interlocked.Increment(ref _renderRevision);
        Dlss5RenderSettings settings = CreateRenderSettings();
        Dlss5RenderCacheKey cacheKey = new(
            Volatile.Read(ref _sourceGeneration),
            _sourceBitmap.Width,
            _sourceBitmap.Height,
            settings);
        if ((TryGetCachedResult(cacheKey, out SKBitmap? cachedBitmap))
            && (cachedBitmap is not null))
        {
            // A cache hit still supersedes an in-flight render (for example
            // when switching back to a previously rendered style).
            _renderScheduler.Cancel();
            _ = PublishResultAsync(
                revision,
                new Dlss5NativeRenderResult(
                    cachedBitmap,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero),
                cacheKey);
            return;
        }

        IsRendering = true;
        _renderScheduler.Request(
            _sourceBitmap,
            settings,
            revision,
            (resultRevision, result) => PublishResultAsync(resultRevision, result, cacheKey),
            ReportRenderFailureAsync);
    }

    private async Task PublishResultAsync(
        long revision,
        Dlss5NativeRenderResult result,
        Dlss5RenderCacheKey cacheKey)
    {
        if (revision != Volatile.Read(ref _renderRevision))
        {
            result.Bitmap.Dispose();
            return;
        }

        PreparedRenderResult prepared;
        try
        {
            prepared = await Task.Run(
                () => PrepareRenderResult(result.Bitmap),
                CancellationToken.None);
        }
        catch (Exception)
        {
            result.Bitmap.Dispose();
            throw;
        }

        if (revision != Volatile.Read(ref _renderRevision))
        {
            prepared.Dispose();
            return;
        }

        try
        {
            await _uiThreadDispatcher.InvokeAsync(
                () =>
                {
                    if (revision != Volatile.Read(ref _renderRevision))
                    {
                        prepared.Dispose();
                        return;
                    }

                    _resultDisplayImage?.Dispose();
                    _resultDisplayImage = prepared.Image;
                    ResultImage = prepared.Image.Value;
                    _resultBitmap?.Dispose();
                    _resultBitmap = prepared.DisplayBitmap;
                    StoreCachedResult(cacheKey, prepared.CacheBitmap);
                    prepared.Detach();
                    IsRendering = false;
                    IsStartingWorker = false;
                    _hasStartedWorker = true;
                    IsOperationProgressIndeterminate = false;
                    OpenComparisonCommand.NotifyCanExecuteChanged();
                },
                CancellationToken.None);
        }
        catch (Exception)
        {
            prepared.Dispose();
            throw;
        }
    }

    private PreparedRenderResult PrepareRenderResult(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        IDlss5DisplayImage? image = null;
        try
        {
            image = _displayImageFactory.Create(bitmap);
            SKBitmap cacheBitmap = bitmap.Copy()
                ?? throw new InvalidDataException("The rendered DLSS 5 image cannot be copied for caching.");
            return new PreparedRenderResult(image, bitmap, cacheBitmap);
        }
        catch (Exception)
        {
            image?.Dispose();
            throw;
        }
    }

    private Task ReportRenderFailureAsync(Exception exception)
    {
        return _uiThreadDispatcher.InvokeAsync(
            () =>
            {
                IsRendering = false;
                IsStartingWorker = false;
                _errorHandler.Log(exception, nameof(QueueRender));
                ErrorMessage = _errorHandler.GetUserMessage(exception);
                _dialogService.ShowLocalizedError(Dlss5LocalizationKeys.Errors.RenderFailed);
            },
            CancellationToken.None);
    }

    private void ApplyState(Dlss5SessionState state)
    {
        Style = state.Style;
        GeneralIntensity = state.GeneralIntensity;
        LocalStructureIntensity = state.LocalStructureIntensity;
        SkinStructureStrength = state.SkinStructureStrength;
        LocalToneStrength = state.LocalToneStrength;
        ParameterAreaHeight = state.ParameterAreaHeight;
    }

    private void SaveAndQueueRender()
    {
        if (_isRestoringState)
        {
            return;
        }

        SaveState();
        CancelSliderWorkerPrewarm();
        // Start the matching v7 worker in the background before the scheduler
        // consumes the request. The dynamic add-on applies every setting to
        // the same live worker, so starting several full-size workers here
        // would only make the GPU compete with itself.
        QueuePrewarm();
        QueueRender();
    }

    private void InvalidateSliderRender()
    {
        if ((_isRestoringState) || (!IsOpen))
        {
            return;
        }

        // Slider values are committed only on release. Invalidate any frame
        // that was started for the previous value so it cannot publish or be
        // cached under the new value while the pointer is still moving.
        Interlocked.Increment(ref _renderRevision);
        _renderScheduler.Cancel();
        // Starting and cancelling native preparation for every pointer event
        // stalls the UI thread. A short debounce still prepares the value when
        // the thumb pauses, while release commits it immediately.
        ScheduleSliderWorkerPrewarm();
        IsRendering = false;
        IsStartingWorker = false;
        IsOperationProgressIndeterminate = false;
    }

    private void SaveState()
    {
        if (_isRestoringState)
        {
            return;
        }

        _stateWriteScheduler.ScheduleWrite(_stateSection, CreateState());
    }

    private void QueuePrewarm()
    {
        if ((_isRestoringState) || (!IsOpen) || (_sourceBitmap is null) || (!_nativeEngine.IsInitialized))
        {
            return;
        }

        CancelPrewarm();
        // A slider commit may already have a worker-only prewarm in flight.
        // PrepareAsync coalesces the same key, so keep that setup alive and
        // let the render await it instead of restarting the process.
        CancellationTokenSource cancellation = new();
        _prewarmCancellation = cancellation;
        int width = _sourceBitmap.Width;
        int height = _sourceBitmap.Height;
        Dlss5RenderSettings settings = CreateRenderSettings();
        _ = PrewarmAsync(cancellation, width, height, settings);
    }

    private async Task PrewarmAsync(
        CancellationTokenSource cancellation,
        int width,
        int height,
        Dlss5RenderSettings settings)
    {
        try
        {
            // Prepare only the worker. The actual source is submitted by
            // the scheduler after the candidate is promoted; keeping a
            // sourceful precomputed frame here could publish pixels from a
            // source that was replaced while setup was finishing.
            await _nativeEngine.PrepareAsync(
                width,
                height,
                settings,
                source: null,
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _errorHandler.Log(exception, nameof(QueuePrewarm));
        }
        finally
        {
            if (ReferenceEquals(_prewarmCancellation, cancellation))
            {
                _prewarmCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelPrewarm()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _prewarmCancellation, null);
        cancellation?.Cancel();
    }

    private void QueueSliderWorkerPrewarm()
    {
        if ((_isRestoringState) || (!IsOpen) || (_sourceBitmap is null) || (!_nativeEngine.IsInitialized))
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        List<CancellationTokenSource> retired = [];
        lock (_sliderPrewarmSync)
        {
            _sliderPrewarmCancellations.AddLast(cancellation);
            while (_sliderPrewarmCancellations.Count > SliderPrewarmCapacity)
            {
                LinkedListNode<CancellationTokenSource> oldest =
                    _sliderPrewarmCancellations.First
                    ?? throw new InvalidOperationException("DLSS 5 slider prewarm queue lost its first item.");
                _sliderPrewarmCancellations.RemoveFirst();
                retired.Add(oldest.Value);
            }
        }

        foreach (CancellationTokenSource stale in retired)
        {
            stale.Cancel();
        }

        int width = _sourceBitmap.Width;
        int height = _sourceBitmap.Height;
        Dlss5RenderSettings settings = CreateRenderSettings();
        _ = PrewarmWorkerAsync(cancellation, width, height, settings);
    }

    private void ScheduleSliderWorkerPrewarm()
    {
        CancellationTokenSource cancellation = new();
        lock (_sliderPrewarmSync)
        {
            _sliderPrewarmDebounceCancellation?.Cancel();
            _sliderPrewarmDebounceCancellation = cancellation;
        }

        _ = QueueSliderWorkerPrewarmAfterDelayAsync(cancellation);
    }

    private async Task QueueSliderWorkerPrewarmAfterDelayAsync(
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(SliderPrewarmDelayMilliseconds, cancellation.Token);
            await _uiThreadDispatcher.InvokeAsync(
                QueueSliderWorkerPrewarm,
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_sliderPrewarmSync)
            {
                if (ReferenceEquals(_sliderPrewarmDebounceCancellation, cancellation))
                {
                    _sliderPrewarmDebounceCancellation = null;
                }

                cancellation.Dispose();
            }
        }
    }

    private async Task PrewarmWorkerAsync(
        CancellationTokenSource cancellation,
        int width,
        int height,
        Dlss5RenderSettings settings)
    {
        try
        {
            await _nativeEngine.PrepareAsync(
                width,
                height,
                settings,
                source: null,
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _errorHandler.Log(exception, nameof(QueueSliderWorkerPrewarm));
        }
        finally
        {
            lock (_sliderPrewarmSync)
            {
                _sliderPrewarmCancellations.Remove(cancellation);
            }

            cancellation.Dispose();
        }
    }

    private void CancelSliderWorkerPrewarm()
    {
        CancelSliderWorkerPrewarmDebounce();
        CancellationTokenSource[] cancellations;
        lock (_sliderPrewarmSync)
        {
            cancellations = _sliderPrewarmCancellations.ToArray();
            _sliderPrewarmCancellations.Clear();
        }

        foreach (CancellationTokenSource cancellation in cancellations)
        {
            cancellation.Cancel();
        }
    }

    private void CancelSliderWorkerPrewarmDebounce()
    {
        lock (_sliderPrewarmSync)
        {
            _sliderPrewarmDebounceCancellation?.Cancel();
            _sliderPrewarmDebounceCancellation = null;
        }
    }

    private Dlss5RenderSettings CreateRenderSettings()
    {
        return new Dlss5RenderSettings(
            Style,
            GeneralIntensity,
            LocalStructureIntensity,
            SkinStructureStrength,
            LocalToneStrength).NormalizeForNative();
    }

    private Dlss5SessionState CreateState()
    {
        return new Dlss5SessionState
        {
            SourceFileName = _sourceFileName,
            Style = Style,
            GeneralIntensity = GeneralIntensity,
            LocalStructureIntensity = LocalStructureIntensity,
            SkinStructureStrength = SkinStructureStrength,
            LocalToneStrength = LocalToneStrength,
            ParameterAreaHeight = ParameterAreaHeight
        };
    }

    private bool CanOpenComparison()
    {
        return (_sourceBitmap is not null) && (_resultBitmap is not null);
    }

    private bool TryGetCachedResult(Dlss5RenderCacheKey key, out SKBitmap? bitmap)
    {
        return _renderCache.TryGet(key, out bitmap);
    }

    private void StoreCachedResult(Dlss5RenderCacheKey key, SKBitmap bitmap)
    {
        _renderCache.Store(key, bitmap);
    }

    private void ClearRenderCache()
    {
        _renderCache.Clear();
    }

    private void ReleaseImageResources()
    {
        _sourceDisplayImage?.Dispose();
        _sourceDisplayImage = null;
        SourceImage = null;
        SourceWidth = 0;
        SourceHeight = 0;
        _sourceBitmap?.Dispose();
        _sourceBitmap = null;

        _resultDisplayImage?.Dispose();
        _resultDisplayImage = null;
        ResultImage = null;
        _resultBitmap?.Dispose();
        _resultBitmap = null;

        ClearRenderCache();
        OpenComparisonCommand.NotifyCanExecuteChanged();
    }

    partial void OnStyleChanged(Dlss5Style value)
    {
        OnPropertyChanged(nameof(StyleIndex));
        SaveAndQueueRender();
    }

    partial void OnGeneralIntensityChanged(float value)
    {
        SaveState();
        InvalidateSliderRender();
    }

    partial void OnLocalStructureIntensityChanged(float value)
    {
        SaveState();
        InvalidateSliderRender();
    }

    partial void OnSkinStructureStrengthChanged(float value)
    {
        SaveState();
        InvalidateSliderRender();
    }

    partial void OnLocalToneStrengthChanged(float value)
    {
        SaveState();
        InvalidateSliderRender();
    }

    partial void OnParameterAreaHeightChanged(double value)
    {
        SaveState();
    }

    private sealed class PreparedRenderResult : IDisposable
    {
        public IDlss5DisplayImage Image { get; }
        public SKBitmap DisplayBitmap { get; }
        public SKBitmap CacheBitmap { get; }

        private bool _detached;

        public PreparedRenderResult(
            IDlss5DisplayImage image,
            SKBitmap displayBitmap,
            SKBitmap cacheBitmap)
        {
            Image = image;
            DisplayBitmap = displayBitmap;
            CacheBitmap = cacheBitmap;
        }

        public void Detach()
        {
            _detached = true;
        }

        public void Dispose()
        {
            if (_detached)
            {
                return;
            }

            Image.Dispose();
            DisplayBitmap.Dispose();
            CacheBitmap.Dispose();
        }
    }

}
