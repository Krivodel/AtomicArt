using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Generation.State;

public sealed class GenerationPanelStateService : IGenerationPanelStateService
{
    private readonly IAppStateStore _stateStore;
    private readonly IStateWriteScheduler _writeScheduler;
    private readonly IImageModelOptionCatalog _imageModelOptionCatalog;
    private readonly GenerationPanelStateSection _section;
    private readonly SemaphoreSlim _stateLock;
    private readonly ILogger<GenerationPanelStateService> _logger;
    private GenerationPanelsState? _currentState;

    public GenerationPanelStateService(
        IAppStateStore stateStore,
        IStateWriteScheduler writeScheduler,
        IImageModelOptionCatalog imageModelOptionCatalog,
        GenerationPanelStateSection section)
        : this(
            stateStore,
            writeScheduler,
            imageModelOptionCatalog,
            section,
            NullLogger<GenerationPanelStateService>.Instance)
    {
    }

    public GenerationPanelStateService(
        IAppStateStore stateStore,
        IStateWriteScheduler writeScheduler,
        IImageModelOptionCatalog imageModelOptionCatalog,
        GenerationPanelStateSection section,
        ILogger<GenerationPanelStateService> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _writeScheduler = writeScheduler ?? throw new ArgumentNullException(nameof(writeScheduler));
        _imageModelOptionCatalog = imageModelOptionCatalog
            ?? throw new ArgumentNullException(nameof(imageModelOptionCatalog));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _stateLock = new SemaphoreSlim(1, 1);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GenerationPanelState> LoadAsync(string panelId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);

        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            GenerationPanelsState state = await GetCurrentStateLockedAsync(ct).ConfigureAwait(false);

            if (!state.Panels.TryGetValue(panelId, out GenerationPanelState? panelState))
            {
                panelState = new GenerationPanelState
                {
                    PanelId = panelId
                };
            }

            GenerationPanelState normalizedState = NormalizePanelState(panelId, panelState);
            _logger.LogInformation(
                "Generation panel state loaded with {AttachmentCount} attachments.",
                normalizedState.Attachments.Count);

            return normalizedState;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task SaveAsync(string panelId, GenerationPanelState state, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(state);

        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            GenerationPanelsState currentState = await GetCurrentStateLockedAsync(ct).ConfigureAwait(false);
            GenerationPanelState normalizedState = NormalizePanelState(panelId, state);
            Dictionary<string, GenerationPanelState> panels =
                new Dictionary<string, GenerationPanelState>(
                    currentState.Panels,
                    StringComparer.Ordinal)
                {
                    [panelId] = normalizedState
                };
            GenerationPanelsState nextState = new()
            {
                Panels = panels
            };

            _currentState = nextState;
            _writeScheduler.ScheduleWrite(_section, nextState);
            _logger.LogDebug(
                "Generation panel state write scheduled with {AttachmentCount} attachments.",
                normalizedState.Attachments.Count);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task<GenerationPanelsState> GetCurrentStateLockedAsync(CancellationToken ct)
    {
        if (_currentState is not null)
        {
            return _currentState;
        }

        GenerationPanelsState loadedState = await _stateStore
            .LoadAsync<GenerationPanelsState>(_section, ct)
            .ConfigureAwait(false);
        _currentState = new GenerationPanelsState
        {
            Panels = new Dictionary<string, GenerationPanelState>(
                loadedState.Panels,
                StringComparer.Ordinal)
        };

        return _currentState;
    }

    private GenerationPanelState NormalizePanelState(string panelId, GenerationPanelState state)
    {
        IReadOnlyList<ImageModelOption> panelModels = _imageModelOptionCatalog
            .GetModels()
            .Where(model => string.Equals(model.PanelId, panelId, StringComparison.Ordinal))
            .ToList();

        if (panelModels.Count == 0)
        {
            return CreateUnavailablePanelState(panelId, state);
        }

        ImageModelOption selectedModel = panelModels.FirstOrDefault(model =>
                string.Equals(model.Id, state.SelectedModelId, StringComparison.Ordinal))
            ?? GenerationPanelOptionDefaults.GetDefaultModel(panelModels);

        return new GenerationPanelState
        {
            PanelId = panelId,
            SelectedModelId = selectedModel.Id,
            AspectRatio = GenerationPanelOptionCompatibility.ResolveString(
                state.AspectRatio,
                selectedModel.AspectRatios,
                GenerationPanelOptionDefaults.GetDefaultAspectRatio(selectedModel))
                .Value,
            Resolution = GenerationPanelOptionCompatibility.ResolveString(
                state.Resolution,
                selectedModel.Resolutions,
                GenerationPanelOptionDefaults.GetDefaultResolution(selectedModel))
                .Value,
            Temperature = GenerationPanelOptionCompatibility.ResolveTemperature(
                state.Temperature,
                selectedModel.Temperature)
                .Value,
            ThinkingLevel = GenerationPanelOptionCompatibility.ResolveRememberedThinkingLevel(
                state.ThinkingLevel,
                selectedModel,
                panelModels)
                .Value,
            GenerationCount = GenerationPanelOptionCompatibility.ResolveGenerationCount(
                state.GenerationCount,
                selectedModel)
                .Value,
            Prompt = state.Prompt ?? string.Empty,
            Attachments = PanelAttachmentStateSanitizer.Sanitize(state.Attachments)
        };
    }

    private static GenerationPanelState CreateUnavailablePanelState(
        string panelId,
        GenerationPanelState state)
    {
        return GenerationPanelStateSanitizer.CreateSanitizedCopy(panelId, state);
    }
}
