using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests.Services.Settings;

public sealed class SettingsStateServiceTests
{
    [Fact]
    public async Task SaveValueAsync_WithNonSecretDefinition_SchedulesStateWithDefinitionKey()
    {
        UiScaleSettingDefinition definition = new();
        RecordingStateWriteScheduler scheduler = new();
        SettingsStateService service = CreateService(
            new SettingsState(),
            scheduler);
        string value = CreateValueConverter().Format(new UiScale125OptionDefinition().Option.Value);

        await service.SaveValueAsync(definition, value, CancellationToken.None);

        SettingsState savedState = scheduler.SavedState.Should().BeOfType<SettingsState>().Subject;
        savedState.Values.Should().ContainKey(definition.Key)
            .WhoseValue.Should().Be(value);
    }

    [Fact]
    public async Task SaveValueAsync_WithExistingSecretAndUnknownKeys_RemovesThemFromScheduledState()
    {
        UiScaleSettingDefinition scaleDefinition = new();
        GoogleApiKeySettingDefinition secretDefinition = new();
        RecordingStateWriteScheduler scheduler = new();
        SettingsState existingState = new()
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [secretDefinition.Key] = "secret",
                ["unknown.setting"] = "unknown",
                [scaleDefinition.Key] = "1.0"
            }
        };
        SettingsStateService service = CreateService(existingState, scheduler);
        string value = CreateValueConverter().Format(new UiScale125OptionDefinition().Option.Value);

        await service.SaveValueAsync(scaleDefinition, value, CancellationToken.None);

        SettingsState savedState = scheduler.SavedState.Should().BeOfType<SettingsState>().Subject;
        savedState.Values.Should().ContainSingle();
        savedState.Values.Should().ContainKey(scaleDefinition.Key)
            .WhoseValue.Should().Be(value);
        savedState.Values.Should().NotContainKey(secretDefinition.Key);
        savedState.Values.Should().NotContainKey("unknown.setting");
    }

    [Fact]
    public async Task SaveValueAsync_WithTwoFastDifferentKeys_SchedulesMergedState()
    {
        UiScaleSettingDefinition scaleDefinition = new();
        SecondaryNonSecretSettingDefinition secondaryDefinition = new();
        RecordingStateWriteScheduler scheduler = new();
        SettingsStateService service = CreateService(
            new SettingsState(),
            scheduler,
            new RecordingUiScaleService(),
            secondaryDefinition);
        string scaleValue = CreateValueConverter().Format(new UiScale125OptionDefinition().Option.Value);

        await service.SaveValueAsync(scaleDefinition, scaleValue, CancellationToken.None);
        await service.SaveValueAsync(secondaryDefinition, SecondaryNonSecretSettingDefinition.Value, CancellationToken.None);

        SettingsState savedState = scheduler.SavedState.Should().BeOfType<SettingsState>().Subject;
        savedState.Values.Should().HaveCount(2);
        savedState.Values.Should().ContainKey(scaleDefinition.Key)
            .WhoseValue.Should().Be(scaleValue);
        savedState.Values.Should().ContainKey(secondaryDefinition.Key)
            .WhoseValue.Should().Be(SecondaryNonSecretSettingDefinition.Value);
    }

    [Fact]
    public void SettingsState_WhenNewSettingIsAdded_DoesNotRequireNewProperty()
    {
        IReadOnlyList<string> propertyNames = typeof(SettingsState)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        propertyNames.Should().Equal(nameof(SettingsState.Values));
    }

    [Fact]
    public async Task SaveValueAsync_WithSecretDefinition_ThrowsAndDoesNotScheduleState()
    {
        RecordingStateWriteScheduler scheduler = new();
        SettingsStateService service = CreateService(
            new SettingsState(),
            scheduler);
        GoogleApiKeySettingDefinition definition = new();

        Func<Task> act = () => service.SaveValueAsync(definition, "secret", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        scheduler.SavedState.Should().BeNull();
    }

    [Fact]
    public async Task SaveValueAsync_WithNonSecretObjectUsingSecretRegisteredKey_ThrowsAndDoesNotScheduleState()
    {
        RecordingStateWriteScheduler scheduler = new();
        SettingsStateService service = CreateService(
            new SettingsState(),
            scheduler);
        SpoofedNonSecretSettingDefinition definition = new();

        Func<Task> act = () => service.SaveValueAsync(definition, "secret", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        scheduler.SavedState.Should().BeNull();
    }

    [Fact]
    public async Task LoadValueAsync_WithSecretAndUnknownKeys_ReturnsOnlyAllowedNonSecretValue()
    {
        UiScaleSettingDefinition definition = new();
        GoogleApiKeySettingDefinition secretDefinition = new();
        string value = CreateValueConverter().Format(new UiScale125OptionDefinition().Option.Value);
        SettingsState state = new()
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [secretDefinition.Key] = "secret",
                ["unknown.setting"] = "unknown",
                [definition.Key] = value
            }
        };
        SettingsStateService service = CreateService(
            state,
            new RecordingStateWriteScheduler());

        string? loadedValue = await service.LoadValueAsync(definition, CancellationToken.None);

        loadedValue.Should().Be(value);
    }

    [Fact]
    public async Task ApplySavedSettingsAsync_WithSupportedScale_AppliesScale()
    {
        UiScaleSettingDefinition definition = new();
        UiScaleOption scaleOption = new UiScale125OptionDefinition().Option;
        RecordingUiScaleService scaleService = new();
        SettingsState state = new()
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [definition.Key] = CreateValueConverter().Format(scaleOption.Value)
            }
        };
        SettingsStateService service = CreateService(
            state,
            new RecordingStateWriteScheduler(),
            scaleService);

        await service.ApplySavedSettingsAsync(CancellationToken.None);

        scaleService.CurrentScale.Should().Be(scaleOption.Value);
    }

    [Fact]
    public async Task ApplySavedSettingsAsync_WithUnsupportedScale_DoesNotApplyScale()
    {
        const string unsupportedScale = "9.99";

        UiScaleSettingDefinition definition = new();
        RecordingUiScaleService scaleService = new();
        double initialScale = scaleService.CurrentScale;
        SettingsState state = new()
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [definition.Key] = unsupportedScale
            }
        };
        SettingsStateService service = CreateService(
            state,
            new RecordingStateWriteScheduler(),
            scaleService);

        await service.ApplySavedSettingsAsync(CancellationToken.None);

        scaleService.CurrentScale.Should().Be(initialScale);
    }

    [Fact]
    public async Task ApplySavedSettingsAsync_WithSavedApiAddress_AppliesAddress()
    {
        ApiBaseAddressSettingDefinition definition = new();
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create();
        SettingsState state = new()
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [definition.Key] = "https://restored.atomicart.test/root"
            }
        };
        SettingsStateService service = CreateApiSettingsService(
            state,
            endpointService);

        await service.ApplySavedSettingsAsync(CancellationToken.None);

        endpointService.BaseAddress.ToString().Should().Be(
            "https://restored.atomicart.test/root/");
    }

    [Fact]
    public async Task ApplySavedSettingsAsync_WithInvalidSavedApiAddress_KeepsConfiguredAddress()
    {
        ApiBaseAddressSettingDefinition definition = new();
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create();
        SettingsState state = new()
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [definition.Key] = "ftp://invalid.atomicart.test/"
            }
        };
        SettingsStateService service = CreateApiSettingsService(
            state,
            endpointService);

        await service.ApplySavedSettingsAsync(CancellationToken.None);

        endpointService.BaseAddress.ToString().Should().Be("https://atomicart.test/");
    }

    private static SettingsStateService CreateService(
        SettingsState state,
        IStateWriteScheduler scheduler)
    {
        return CreateService(
            state,
            scheduler,
            new RecordingUiScaleService());
    }

    private static SettingsStateService CreateService(
        SettingsState state,
        IStateWriteScheduler scheduler,
        IUiScaleService scaleService)
    {
        return CreateService(
            state,
            scheduler,
            scaleService,
            []);
    }

    private static SettingsStateService CreateService(
        SettingsState state,
        IStateWriteScheduler scheduler,
        IUiScaleService scaleService,
        params ISettingsDefinition[] additionalSettings)
    {
        ISettingsDefinition[] settings =
        [
            new GoogleApiKeySettingDefinition(),
            new UiScaleSettingDefinition(),
            ..additionalSettings
        ];
        IUiScaleOptionDefinition[] scaleOptions =
        [
            new UiScale125OptionDefinition()
        ];
        SettingsDefinitionCatalog catalog = new(
            settings,
            scaleOptions);
        IUiScaleSettingValueConverter valueConverter = CreateValueConverter();
        ISettingsStateApplicator[] applicators =
        [
            new UiScaleSettingsStateApplicator(catalog, scaleService, valueConverter)
        ];

        return new SettingsStateService(
            new StubAppStateStore(state),
            scheduler,
            catalog,
            new SettingsStateSection(),
            applicators);
    }

    private static IUiScaleSettingValueConverter CreateValueConverter()
    {
        return new UiScaleSettingValueConverter();
    }

    private static SettingsStateService CreateApiSettingsService(
        SettingsState state,
        IApiEndpointService endpointService)
    {
        ApiBaseAddressSettingDefinition definition = new();
        SettingsDefinitionCatalog catalog = new(
            [
                new GoogleApiKeySettingDefinition(),
                definition
            ],
            []);
        ISettingsStateApplicator[] applicators =
        [
            new ApiBaseAddressSettingsStateApplicator(catalog, endpointService)
        ];

        return new SettingsStateService(
            new StubAppStateStore(state),
            new RecordingStateWriteScheduler(),
            catalog,
            new SettingsStateSection(),
            applicators);
    }

    private sealed class StubAppStateStore : IAppStateStore
    {
        private readonly SettingsState _state;

        public StubAppStateStore(SettingsState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public Task<TState> LoadAsync<TState>(IStateSection section, CancellationToken ct)
        {
            if (_state is TState typedState)
            {
                return Task.FromResult(typedState);
            }

            throw new InvalidOperationException("Unexpected test state type.");
        }

        public Task SaveAsync<TState>(IStateSection section, TState state, CancellationToken ct)
            where TState : notnull
        {
            return SaveAsync(section, (object)state, ct);
        }

        public Task SaveAsync(IStateSection section, object state, CancellationToken ct)
        {
            throw new NotSupportedException("Direct saving is not used by this test.");
        }
    }

    private sealed class RecordingStateWriteScheduler : IStateWriteScheduler
    {
        public object? SavedState { get; private set; }

        public void ScheduleWrite<TState>(
            IStateSection section,
            TState state,
            StateWriteMode mode = StateWriteMode.Deferred)
            where TState : notnull
        {
            SavedState = state;
        }

        public Task FlushAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUiScaleService : IUiScaleService
    {
        private const double InitialScale = 0.75;

        public double CurrentScale { get; private set; } = InitialScale;

        public event EventHandler? ScaleChanged;

        public void SetScale(double scale)
        {
            CurrentScale = scale;
            ScaleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class SpoofedNonSecretSettingDefinition : ISettingsDefinition
    {
        public string Key => GoogleApiKeySettingDefinition.KeyValue;
        public int Order => 300;
    }

    private sealed class SecondaryNonSecretSettingDefinition : ISettingsDefinition
    {
        public const string Value = "secondary-value";

        public string Key => "test.secondary";
        public int Order => 400;
    }
}
