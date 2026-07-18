using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task ApplyScale_WithScaleSetting_UpdatesScaleService()
    {
        RecordingUiScaleService scaleService = new();
        SettingsViewModel viewModel = CreateViewModel(
            new RecordingSecretStore(),
            scaleService,
            new TestViewModelErrorHandler());
        ScaleSettingViewModel scaleSetting = viewModel.Settings.OfType<ScaleSettingViewModel>().Single();
        UiScaleOption scaleOption = new UiScale125OptionDefinition().Option;
        scaleSetting.SelectedOption = scaleSetting.Options.Single(option => option == scaleOption);

        await scaleSetting.ApplyCommand.ExecuteAsync(null);

        scaleService.CurrentScale.Should().Be(scaleOption.Value);
    }

    [Fact]
    public async Task ApplyScale_WithScaleSetting_SavesScaleState()
    {
        RecordingSettingsStateService settingsStateService = new();
        SettingsViewModel viewModel = CreateViewModel(
            new RecordingSecretStore(),
            new RecordingUiScaleService(),
            new TestViewModelErrorHandler(),
            settingsStateService);
        ScaleSettingViewModel scaleSetting = viewModel.Settings.OfType<ScaleSettingViewModel>().Single();
        UiScaleOption scaleOption = new UiScale125OptionDefinition().Option;
        scaleSetting.SelectedOption = scaleSetting.Options.Single(option => option == scaleOption);

        await scaleSetting.ApplyCommand.ExecuteAsync(null);

        settingsStateService.SavedScaleKey.Should().Be(UiScaleSettingDefinition.KeyValue);
        settingsStateService.SavedValue.Should().Be(
            new UiScaleSettingValueConverter().Format(scaleOption.Value));
    }

    [Fact]
    public async Task SaveSecretAsync_WithValue_CallsSecretStore()
    {
        RecordingSecretStore secretStore = new();
        SettingsViewModel viewModel = CreateViewModel(
            secretStore,
            new RecordingUiScaleService(),
            new TestViewModelErrorHandler());
        SecretSettingViewModel secretSetting = viewModel.Settings.OfType<SecretSettingViewModel>().Single();
        secretSetting.Value = "value-for-test-only";

        await secretSetting.SaveCommand.ExecuteAsync(null);

        secretStore.StoredValues[secretSetting.SecretName].Should().Be("value-for-test-only");
        secretSetting.Value.Should().BeEmpty();
        secretSetting.HasErrorMessage.Should().BeFalse();
    }

    [Fact]
    public async Task SaveSecretAsync_WithTwoSecretSettings_SavesSelectedSecret()
    {
        RecordingSecretStore secretStore = new();
        SettingsViewModel viewModel = CreateViewModel(
            secretStore,
            new RecordingUiScaleService(),
            new TestViewModelErrorHandler(),
            new SecondarySecretSettingDefinition());
        SecretSettingViewModel secretSetting = viewModel.Settings.OfType<SecretSettingViewModel>()
            .Single(setting => setting.SecretName == SecondarySecretSettingDefinition.SecretNameValue);
        secretSetting.Value = "second-value";

        await secretSetting.SaveCommand.ExecuteAsync(null);

        viewModel.Settings.OfType<SecretSettingViewModel>().Should().HaveCount(2);
        secretStore.StoredValues[SecondarySecretSettingDefinition.SecretNameValue].Should().Be("second-value");
    }

    [Fact]
    public async Task SaveSecretAsync_WhenStoreThrows_DoesNotExposeKey()
    {
        ThrowingSecretStore secretStore = new();
        TestViewModelErrorHandler errorHandler = new();
        SettingsViewModel viewModel = CreateViewModel(
            secretStore,
            new RecordingUiScaleService(),
            errorHandler);
        SecretSettingViewModel secretSetting = viewModel.Settings.OfType<SecretSettingViewModel>().Single();
        secretSetting.Value = "value-for-test-only";

        await secretSetting.SaveCommand.ExecuteAsync(null);

        errorHandler.LogCallCount.Should().Be(1);
        secretSetting.ErrorMessage.Should().Be(UiStrings.GenerationFailed);
        secretSetting.ErrorMessage.Should().NotContain("value-for-test-only");
        secretSetting.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task SaveSecretAsync_WhenStoreThrowsUnexpectedCancellation_LogsAndSetsErrorMessage()
    {
        UnexpectedCancellationSecretStore secretStore = new();
        TestViewModelErrorHandler errorHandler = new();
        SettingsViewModel viewModel = CreateViewModel(
            secretStore,
            new RecordingUiScaleService(),
            errorHandler);
        SecretSettingViewModel secretSetting = viewModel.Settings.OfType<SecretSettingViewModel>().Single();

        await secretSetting.SaveCommand.ExecuteAsync(null);

        errorHandler.LogCallCount.Should().Be(1);
        secretSetting.ErrorMessage.Should().Be(UiStrings.UnhandledExceptionMessage);
        secretSetting.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyScale_WhenScaleServiceThrows_LogsAndSetsErrorMessage()
    {
        ThrowingUiScaleService scaleService = new();
        TestViewModelErrorHandler errorHandler = new();
        SettingsViewModel viewModel = CreateViewModel(
            new RecordingSecretStore(),
            scaleService,
            errorHandler);
        ScaleSettingViewModel scaleSetting = viewModel.Settings.OfType<ScaleSettingViewModel>().Single();

        await scaleSetting.ApplyCommand.ExecuteAsync(null);

        errorHandler.LogCallCount.Should().Be(1);
        scaleSetting.ErrorMessage.Should().Be(UiStrings.GenerationFailed);
    }

    [Fact]
    public void CreateSettings_WithEmptyScaleOptions_DoesNotThrow()
    {
        SettingsViewModel viewModel = CreateViewModel(
            new RecordingSecretStore(),
            new RecordingUiScaleService(),
            new TestViewModelErrorHandler(),
            scaleOptions: []);

        ScaleSettingViewModel scaleSetting = viewModel.Settings.OfType<ScaleSettingViewModel>().Single();

        scaleSetting.Options.Should().BeEmpty();
        scaleSetting.SelectedOption.Should().BeNull();
        scaleSetting.ApplyCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CreateSettings_WithGoogleApiKeySetting_UsesGoogleSecretDefinition()
    {
        SettingsViewModel viewModel = CreateViewModel(
            new RecordingSecretStore(),
            new RecordingUiScaleService(),
            new TestViewModelErrorHandler());

        SecretSettingViewModel secretSetting = viewModel.Settings
            .OfType<SecretSettingViewModel>()
            .Single();

        secretSetting.Key.Should().Be(GoogleApiKeySettingDefinition.KeyValue);
        secretSetting.SecretName.Should().Be(GoogleApiKeySettingDefinition.SecretNameValue);
        secretSetting.DisplayName.Should().Be(UiStrings.SettingsGoogleApiKeyLabel);
    }

    [Fact]
    public void CreateSettings_WithGoogleApiKeySetting_DoesNotExposeNanoBanana2Secret()
    {
        SettingsViewModel viewModel = CreateViewModel(
            new RecordingSecretStore(),
            new RecordingUiScaleService(),
            new TestViewModelErrorHandler());

        IReadOnlyList<SecretSettingViewModel> secretSettings = viewModel.Settings
            .OfType<SecretSettingViewModel>()
            .ToList();

        secretSettings.Should().NotContain(setting => setting.Key == "generation.nanoBanana2.apiKey");
        secretSettings.Should().NotContain(setting => setting.SecretName == "NanoBanana2ApiKey");
    }

    private sealed class RecordingSecretStore : ISecretStore
    {
        public Dictionary<string, string> StoredValues { get; } = [];

        public Task<string?> GetSecretAsync(string key, CancellationToken ct)
        {
            StoredValues.TryGetValue(key, out string? value);

            return Task.FromResult(value);
        }

        public Task SetSecretAsync(string key, string value, CancellationToken ct)
        {
            StoredValues[key] = value;

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSecretStore : ISecretStore
    {
        public Task<string?> GetSecretAsync(string key, CancellationToken ct)
        {
            return Task.FromResult<string?>(null);
        }

        public Task SetSecretAsync(string key, string value, CancellationToken ct)
        {
            throw new InvalidOperationException("Failed");
        }
    }

    private sealed class UnexpectedCancellationSecretStore : ISecretStore
    {
        public Task<string?> GetSecretAsync(string key, CancellationToken ct)
        {
            return Task.FromResult<string?>(null);
        }

        public Task SetSecretAsync(string key, string value, CancellationToken ct)
        {
            throw new OperationCanceledException();
        }
    }

    private static SettingsViewModel CreateViewModel(
        ISecretStore secretStore,
        IUiScaleService uiScaleService,
        TestViewModelErrorHandler errorHandler,
        IUiScaleOptionDefinition[] scaleOptions)
    {
        return CreateViewModel(
            secretStore,
            uiScaleService,
            errorHandler,
            new RecordingSettingsStateService(uiScaleService),
            [],
            scaleOptions);
    }

    private static SettingsViewModel CreateViewModel(
        ISecretStore secretStore,
        IUiScaleService uiScaleService,
        TestViewModelErrorHandler errorHandler,
        params ISettingsDefinition[] additionalSettings)
    {
        return CreateViewModel(
            secretStore,
            uiScaleService,
            errorHandler,
            new RecordingSettingsStateService(uiScaleService),
            additionalSettings,
            CreateRequiredScaleOptions());
    }

    private static SettingsViewModel CreateViewModel(
        ISecretStore secretStore,
        IUiScaleService uiScaleService,
        TestViewModelErrorHandler errorHandler,
        ISettingsStateService settingsStateService,
        params ISettingsDefinition[] additionalSettings)
    {
        return CreateViewModel(
            secretStore,
            uiScaleService,
            errorHandler,
            settingsStateService,
            additionalSettings,
            CreateRequiredScaleOptions());
    }

    private static SettingsViewModel CreateViewModel(
        ISecretStore secretStore,
        IUiScaleService uiScaleService,
        TestViewModelErrorHandler errorHandler,
        ISettingsStateService settingsStateService,
        ISettingsDefinition[] additionalSettings,
        IUiScaleOptionDefinition[] scaleOptions)
    {
        SettingsDefinitionCatalog catalog = CreateSettingsDefinitionCatalog(
            additionalSettings,
            scaleOptions);
        ISettingsItemViewModelFactory[] factories = CreateSettingFactories(
            secretStore,
            uiScaleService,
            errorHandler,
            catalog,
            settingsStateService);
        SettingsItemViewModelProvider provider = new(catalog, factories);

        return new SettingsViewModel(provider);
    }

    private static SettingsDefinitionCatalog CreateSettingsDefinitionCatalog(
        ISettingsDefinition[] additionalSettings,
        IUiScaleOptionDefinition[] scaleOptions)
    {
        ISettingsDefinition[] settings =
        [
            new GoogleApiKeySettingDefinition(),
            new UiScaleSettingDefinition(),
            ..additionalSettings
        ];

        return new SettingsDefinitionCatalog(settings, scaleOptions);
    }

    private static IUiScaleOptionDefinition[] CreateRequiredScaleOptions()
    {
        return
        [
            new UiScale125OptionDefinition()
        ];
    }

    private static ISettingsItemViewModelFactory[] CreateSettingFactories(
        ISecretStore secretStore,
        IUiScaleService uiScaleService,
        TestViewModelErrorHandler errorHandler,
        ISettingsDefinitionCatalog catalog,
        ISettingsStateService settingsStateService)
    {
        return
        [
            new SecretSettingViewModelFactory(secretStore, errorHandler),
            new ScaleSettingViewModelFactory(
                catalog,
                uiScaleService,
                settingsStateService,
                new UiScaleSettingValueConverter(),
                errorHandler)
        ];
    }

    private sealed class SecondarySecretSettingDefinition : ISecretSettingDefinition
    {
        public const string SecretNameValue = "SecondarySecret";

        public string Key => "test.secondarySecret";
        public int Order => 150;
        public string SecretName => SecretNameValue;
        public string DisplayName => "Второй ключ";
        public string Placeholder => "Второе значение";
        public string SaveButtonText => "Сохранить";
    }

    private sealed class RecordingSettingsStateService : ISettingsStateService
    {
        private readonly IUiScaleService? _uiScaleService;

        public string? SavedScaleKey { get; private set; }
        public string? SavedValue { get; private set; }

        public RecordingSettingsStateService()
        {
        }

        public RecordingSettingsStateService(IUiScaleService uiScaleService)
        {
            _uiScaleService = uiScaleService ?? throw new ArgumentNullException(nameof(uiScaleService));
        }

        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
            if (definition is UiScaleSettingDefinition
                && _uiScaleService is not null
                && new UiScaleSettingValueConverter().TryParse(value, out double scale))
            {
                _uiScaleService.SetScale(scale);
            }
        }

        public Task<string?> LoadValueAsync(ISettingsDefinition definition, CancellationToken ct)
        {
            return Task.FromResult<string?>(null);
        }

        public Task SaveValueAsync(ISettingsDefinition definition, string value, CancellationToken ct)
        {
            SavedScaleKey = definition.Key;
            SavedValue = value;

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingUiScaleService : IUiScaleService
    {
        public double CurrentScale => UiScaleDefaults.DefaultScale;

        public event EventHandler? ScaleChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public void SetScale(double scale)
        {
            throw new InvalidOperationException("Scale failed");
        }
    }
}
