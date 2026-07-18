using System.Text.Json;
using Microsoft.Extensions.Logging;

using Pica.Protocol;

namespace Pica.Viewer.Services;

public sealed class ImageViewerStateService : IImageViewerStateService
{
    private const string StateDirectoryName = "State";
    private const string StateFileName = "image-viewer.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly string _stateFilePath;
    private readonly ILogger<ImageViewerStateService> _logger;
    private ImageViewerState? _currentState;

    public ImageViewerStateService(ILogger<ImageViewerStateService> logger)
        : this(CreateDefaultStateFilePath(), logger)
    {
    }

    internal ImageViewerStateService(
        string stateFilePath,
        ILogger<ImageViewerStateService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFilePath);
        ArgumentNullException.ThrowIfNull(logger);

        _stateFilePath = stateFilePath;
        _logger = logger;
    }

    public async Task<ImageViewerState> LoadAsync(CancellationToken ct)
    {
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_currentState is not null)
            {
                _logger.LogDebug("Returning cached Pica viewer state");
                return _currentState;
            }

            if (!File.Exists(_stateFilePath))
            {
                _currentState = new ImageViewerState();
                _logger.LogInformation("Pica viewer state does not exist; using defaults");
                return _currentState;
            }

            await using FileStream stream = new(
                _stateFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous);
            ImageViewerState? state = await JsonSerializer
                .DeserializeAsync<ImageViewerState>(stream, SerializerOptions, ct)
                .ConfigureAwait(false);
            _currentState = Normalize(state);
            _logger.LogInformation("Loaded and normalized Pica viewer state");

            return _currentState;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Pica viewer state is invalid; using defaults");
            _currentState = new ImageViewerState();
            return _currentState;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task SaveAsync(ImageViewerState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            ImageViewerState normalizedState = Normalize(state);
            string? directoryPath = Path.GetDirectoryName(_stateFilePath);

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new InvalidOperationException("The Pica state directory could not be determined.");
            }

            Directory.CreateDirectory(directoryPath);
            await using FileStream stream = new(
                _stateFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous);
            await JsonSerializer
                .SerializeAsync(stream, normalizedState, SerializerOptions, ct)
                .ConfigureAwait(false);
            _currentState = normalizedState;
            _logger.LogInformation(
                "Saved Pica viewer state with remembered window placement {RememberWindowPlacement}",
                normalizedState.RememberWindowPlacement);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private static ImageViewerState Normalize(ImageViewerState? state)
    {
        bool rememberWindowPlacement = state?.RememberWindowPlacement
            ?? ViewerSettingsDefaults.RememberWindowPlacement;
        bool smoothPanningEnabled = state?.IsSmoothPanningEnabled
            ?? ViewerSettingsDefaults.SmoothPanningEnabled;

        return new ImageViewerState
        {
            IsFilteringEnabled = state?.IsFilteringEnabled ?? true,
            MovementSpeed = ViewerSettingsDefaults.NormalizeSpeed(
                state?.MovementSpeed ?? ViewerSettingsDefaults.MovementSpeed,
                ViewerSettingsDefaults.MovementSpeed),
            ZoomSpeed = ViewerSettingsDefaults.NormalizeSpeed(
                state?.ZoomSpeed ?? ViewerSettingsDefaults.ZoomSpeed,
                ViewerSettingsDefaults.ZoomSpeed),
            ExpandOnDoubleClick = state?.ExpandOnDoubleClick
                ?? ViewerSettingsDefaults.ExpandOnDoubleClick,
            IsFastLoadingEnabled = state?.IsFastLoadingEnabled
                ?? ViewerSettingsDefaults.FastLoadingEnabled,
            AllowFreeZoomOut = state?.AllowFreeZoomOut
                ?? ViewerSettingsDefaults.AllowFreeZoomOut,
            IsSmoothPanningEnabled = smoothPanningEnabled,
            IsPanningInertiaEnabled = smoothPanningEnabled
                && (state?.IsPanningInertiaEnabled
                    ?? ViewerSettingsDefaults.PanningInertiaEnabled),
            ResizeBehavior = ViewerSettingsDefaults.NormalizeResizeBehavior(
                state?.ResizeBehavior ?? ViewerSettingsDefaults.ResizeBehavior),
            RememberWindowPlacement = rememberWindowPlacement,
            IsWindowed = rememberWindowPlacement
                && (state?.IsWindowed ?? HasCompleteWindowPlacement(state)),
            WindowX = rememberWindowPlacement ? state?.WindowX : null,
            WindowY = rememberWindowPlacement ? state?.WindowY : null,
            WindowWidth = rememberWindowPlacement ? NormalizeDimension(state?.WindowWidth) : null,
            WindowHeight = rememberWindowPlacement ? NormalizeDimension(state?.WindowHeight) : null
        };
    }

    private static double? NormalizeDimension(double? dimension)
    {
        return dimension is > 0d && double.IsFinite(dimension.Value)
            ? dimension
            : null;
    }

    private static bool HasCompleteWindowPlacement(ImageViewerState? state)
    {
        return state?.WindowX is not null
            && state.WindowY is not null
            && state.WindowWidth is not null
            && state.WindowHeight is not null;
    }

    private static string CreateDefaultStateFilePath()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localApplicationData,
            PicaProtocolConstants.ApplicationName,
            StateDirectoryName,
            StateFileName);
    }
}
