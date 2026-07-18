using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class SettingsStateService : ISettingsStateService
{
    private readonly IAppStateStore _stateStore;
    private readonly IStateWriteScheduler _writeScheduler;
    private readonly ISettingsDefinitionCatalog _settingsDefinitionCatalog;
    private readonly IReadOnlyDictionary<string, ISettingsStateApplicator> _applicatorsByKey;
    private readonly SettingsStateSection _section;
    private readonly SemaphoreSlim _stateLock;
    private readonly ILogger<SettingsStateService> _logger;
    private SettingsState? _currentState;

    public SettingsStateService(
        IAppStateStore stateStore,
        IStateWriteScheduler writeScheduler,
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        SettingsStateSection section,
        IEnumerable<ISettingsStateApplicator> applicators)
        : this(
            stateStore,
            writeScheduler,
            settingsDefinitionCatalog,
            section,
            applicators,
            NullLogger<SettingsStateService>.Instance)
    {
    }

    public SettingsStateService(
        IAppStateStore stateStore,
        IStateWriteScheduler writeScheduler,
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        SettingsStateSection section,
        IEnumerable<ISettingsStateApplicator> applicators,
        ILogger<SettingsStateService> logger)
    {
        ArgumentNullException.ThrowIfNull(applicators);

        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _writeScheduler = writeScheduler ?? throw new ArgumentNullException(nameof(writeScheduler));
        _settingsDefinitionCatalog = settingsDefinitionCatalog
            ?? throw new ArgumentNullException(nameof(settingsDefinitionCatalog));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _applicatorsByKey = CreateApplicatorRegistry(applicators);
        _stateLock = new SemaphoreSlim(1, 1);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ApplySavedSettingsAsync(CancellationToken ct)
    {
        SettingsState state = await GetCurrentStateAsync(ct).ConfigureAwait(false);

        foreach (KeyValuePair<string, string> value in state.Values)
        {
            ApplyValueIfSupported(value.Key, value.Value);
        }

        _logger.LogInformation(
            "Applied {AppliedSettingCount} saved non-secret settings.",
            state.Values.Count);
    }

    public void ApplyValue(ISettingsDefinition definition, string value)
    {
        ISettingsDefinition registeredDefinition = GetRegisteredNonSecretDefinition(definition);
        ArgumentNullException.ThrowIfNull(value);

        if (!_applicatorsByKey.TryGetValue(
                registeredDefinition.Key,
                out ISettingsStateApplicator? applicator))
        {
            throw new InvalidOperationException(
                $"Settings definition '{registeredDefinition.Key}' does not support immediate application.");
        }

        applicator.Apply(value);
        _logger.LogInformation(
            "Non-secret setting {SettingKey} was applied.",
            registeredDefinition.Key);
    }

    public async Task<string?> LoadValueAsync(ISettingsDefinition definition, CancellationToken ct)
    {
        ISettingsDefinition registeredDefinition = GetRegisteredNonSecretDefinition(definition);
        SettingsState state = await GetCurrentStateAsync(ct).ConfigureAwait(false);

        if (state.Values.TryGetValue(registeredDefinition.Key, out string? value))
        {
            return value;
        }

        return null;
    }

    public async Task SaveValueAsync(ISettingsDefinition definition, string value, CancellationToken ct)
    {
        ISettingsDefinition registeredDefinition = GetRegisteredNonSecretDefinition(definition);
        ArgumentNullException.ThrowIfNull(value);

        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            SettingsState state = await GetCurrentStateLockedAsync(ct).ConfigureAwait(false);
            Dictionary<string, string> values = new Dictionary<string, string>(
                state.Values,
                StringComparer.Ordinal)
            {
                [registeredDefinition.Key] = value
            };
            SettingsState nextState = new()
            {
                Values = values
            };

            _currentState = nextState;
            _writeScheduler.ScheduleWrite(_section, nextState);
            _logger.LogInformation(
                "Non-secret setting {SettingKey} state write was scheduled.",
                registeredDefinition.Key);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task<SettingsState> GetCurrentStateAsync(CancellationToken ct)
    {
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await GetCurrentStateLockedAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task<SettingsState> GetCurrentStateLockedAsync(CancellationToken ct)
    {
        if (_currentState is not null)
        {
            return _currentState;
        }

        SettingsState loadedState = await _stateStore.LoadAsync<SettingsState>(_section, ct)
            .ConfigureAwait(false);
        _currentState = FilterAllowedState(loadedState);
        _logger.LogInformation(
            "Loaded {SettingCount} allowed non-secret settings from state.",
            _currentState.Values.Count);

        return _currentState;
    }

    private SettingsState FilterAllowedState(SettingsState state)
    {
        IReadOnlySet<string> allowedKeys = GetAllowedNonSecretKeys();
        Dictionary<string, string> values = state.Values
            .Where(pair => allowedKeys.Contains(pair.Key) && pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return new SettingsState
        {
            Values = values
        };
    }

    private IReadOnlyDictionary<string, ISettingsStateApplicator> CreateApplicatorRegistry(
        IEnumerable<ISettingsStateApplicator> applicators)
    {
        Dictionary<string, ISettingsStateApplicator> applicatorsByKey =
            new Dictionary<string, ISettingsStateApplicator>(StringComparer.Ordinal);
        IReadOnlySet<string> allowedKeys = GetAllowedNonSecretKeys();

        foreach (ISettingsStateApplicator applicator in applicators)
        {
            ArgumentNullException.ThrowIfNull(applicator);
            ArgumentException.ThrowIfNullOrWhiteSpace(applicator.SettingKey);

            if (!allowedKeys.Contains(applicator.SettingKey))
            {
                throw new InvalidOperationException(
                    $"Settings applicator key '{applicator.SettingKey}' is not a registered non-secret setting.");
            }

            if (!applicatorsByKey.TryAdd(applicator.SettingKey, applicator))
            {
                throw new InvalidOperationException(
                    $"Settings applicator key '{applicator.SettingKey}' is registered more than once.");
            }
        }

        return applicatorsByKey;
    }

    private ISettingsDefinition GetRegisteredNonSecretDefinition(ISettingsDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        List<ISettingsDefinition> matchingDefinitions = _settingsDefinitionCatalog
            .GetSettings()
            .Where(setting => string.Equals(setting.Key, definition.Key, StringComparison.Ordinal))
            .ToList();

        if (matchingDefinitions.Count == 0)
        {
            throw new InvalidOperationException(
                $"Settings definition '{definition.Key}' is not registered.");
        }

        if (matchingDefinitions.Count > 1)
        {
            throw new InvalidOperationException(
                $"Settings definition key '{definition.Key}' is registered more than once.");
        }

        ISettingsDefinition registeredDefinition = matchingDefinitions[0];

        if (registeredDefinition is ISecretSettingDefinition)
        {
            throw new InvalidOperationException(
                "Secret settings must be stored through the protected secret store.");
        }

        return registeredDefinition;
    }

    private IReadOnlySet<string> GetAllowedNonSecretKeys()
    {
        return GetAllowedNonSecretDefinitions()
            .Select(setting => setting.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private IReadOnlyList<ISettingsDefinition> GetAllowedNonSecretDefinitions()
    {
        return _settingsDefinitionCatalog.GetSettings()
            .Where(setting => setting is not ISecretSettingDefinition)
            .ToList();
    }

    private void ApplyValueIfSupported(string settingKey, string value)
    {
        if (_applicatorsByKey.TryGetValue(settingKey, out ISettingsStateApplicator? applicator))
        {
            applicator.Apply(value);
        }
    }
}
