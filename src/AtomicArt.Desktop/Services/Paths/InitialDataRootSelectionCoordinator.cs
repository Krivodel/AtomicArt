using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services.Paths;

public sealed class InitialDataRootSelectionCoordinator
{
    private readonly AtomicArtDataRootBootstrapStore _bootstrapStore;
    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly IDialogService _dialogService;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly ILogger<InitialDataRootSelectionCoordinator> _logger;

    public InitialDataRootSelectionCoordinator(
        AtomicArtDataRootBootstrapStore bootstrapStore,
        IAtomicArtDataPathProvider pathProvider,
        IDialogService dialogService,
        IUiThreadDispatcher uiThreadDispatcher,
        ILogger<InitialDataRootSelectionCoordinator> logger)
    {
        _bootstrapStore = bootstrapStore
            ?? throw new ArgumentNullException(nameof(bootstrapStore));
        _pathProvider = pathProvider
            ?? throw new ArgumentNullException(nameof(pathProvider));
        _dialogService = dialogService
            ?? throw new ArgumentNullException(nameof(dialogService));
        _uiThreadDispatcher = uiThreadDispatcher
            ?? throw new ArgumentNullException(nameof(uiThreadDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OfferAsync(
        Func<Task> changeDirectoryAsync,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(changeDirectoryAsync);

        string rootDirectory = _pathProvider.RootDirectory;
        await PersistStateAsync(
                () => _bootstrapStore.MarkInitialRootDirectorySelectionPendingAsync(
                    rootDirectory,
                    ct),
                "marking as pending")
            .ConfigureAwait(false);

        bool isConfirmed = false;
        LocalizedConfirmationDialogRequest request = CreateRequest(rootDirectory);
        await _uiThreadDispatcher.InvokeAsync(
                async () =>
                {
                    isConfirmed = await _dialogService.ShowConfirmationAsync(
                        request,
                        ct);
                },
                ct)
            .ConfigureAwait(false);

        await PersistStateAsync(
                () => _bootstrapStore.MarkInitialRootDirectorySelectionCompletedAsync(
                    rootDirectory,
                    ct),
                "marking as completed")
            .ConfigureAwait(false);

        if (isConfirmed)
        {
            await _uiThreadDispatcher
                .InvokeAsync(changeDirectoryAsync, ct)
                .ConfigureAwait(false);
        }
    }

    private static LocalizedConfirmationDialogRequest CreateRequest(
        string rootDirectory)
    {
        object?[] messageArguments = [rootDirectory];

        return new LocalizedConfirmationDialogRequest(
            SettingsLocalizationKeys.DataRoot.Label,
            SettingsLocalizationKeys.DataRoot.InitialSelectionMessage,
            CommonLocalizationKeys.Yes,
            CommonLocalizationKeys.NotNow,
            ConfirmationDialogKind.Standard,
            ConfirmationDialogBackgroundClickBehavior.Ignore,
            messageArguments);
    }

    private async Task PersistStateAsync(
        Func<Task> persistState,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(persistState);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        try
        {
            await persistState().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            _logger.LogWarning(
                ex,
                "Initial data root selection state could not be persisted during {OperationName}.",
                operationName);
        }
    }
}
