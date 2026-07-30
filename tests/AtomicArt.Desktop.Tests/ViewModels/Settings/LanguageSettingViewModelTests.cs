using System.Collections.Specialized;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class LanguageSettingViewModelTests
{
    [Fact]
    public void Constructor_WithAvailableLocalizations_SelectsCurrentVariantByFilename()
    {
        LocalizationOption english = new(
            "English",
            new System.Globalization.CultureInfo("en-US"),
            true);
        LocalizationOption custom = new(
            "English concise",
            new System.Globalization.CultureInfo("en-US"),
            false);
        TestLocalizationService localizationService = new()
        {
            AvailableLocalizations = new List<LocalizationOption>
            {
                english,
                custom
            },
            CurrentLocalization = custom
        };

        LanguageSettingViewModel viewModel = CreateViewModel(
            localizationService,
            new RecordingSettingsStateService());

        viewModel.Options
            .Select(option => option.Localization)
            .Should()
            .Equal(english, custom);
        viewModel.SelectedOption?.Localization.Should().BeSameAs(custom);
    }

    [Fact]
    public void SearchText_WithPartialCaseInsensitiveText_UpdatesOptionVisibility()
    {
        LocalizationOption english = new(
            "English",
            new System.Globalization.CultureInfo("en-US"),
            true);
        LocalizationOption russian = new(
            "Русский",
            new System.Globalization.CultureInfo("ru-RU"),
            true);
        LocalizationOption japanese = new(
            "日本語",
            new System.Globalization.CultureInfo("ja-JP"),
            false);
        TestLocalizationService localizationService = new()
        {
            AvailableLocalizations = new List<LocalizationOption>
            {
                english,
                russian,
                japanese
            },
            CurrentLocalization = english
        };
        LanguageSettingViewModel viewModel = CreateViewModel(
            localizationService,
            new RecordingSettingsStateService());

        viewModel.SearchText = "РУС";

        viewModel.Options.Single(option => option.Localization == english)
            .IsSearchMatch.Should().BeFalse();
        viewModel.Options.Single(option => option.Localization == russian)
            .IsSearchMatch.Should().BeTrue();
        viewModel.Options.Single(option => option.Localization == japanese)
            .IsSearchMatch.Should().BeFalse();
    }

    [Fact]
    public void SearchText_WhenSelectedOptionDoesNotMatch_PreservesSelectionAndDoesNotPersist()
    {
        LocalizationOption english = new(
            "English",
            new System.Globalization.CultureInfo("en-US"),
            true);
        LocalizationOption russian = new(
            "Русский",
            new System.Globalization.CultureInfo("ru-RU"),
            true);
        TestLocalizationService localizationService = new()
        {
            AvailableLocalizations = new List<LocalizationOption>
            {
                english,
                russian
            },
            CurrentLocalization = english
        };
        RecordingSettingsStateService settingsStateService = new();
        LanguageSettingViewModel viewModel = CreateViewModel(
            localizationService,
            settingsStateService);

        viewModel.SearchText = "Рус";

        viewModel.SelectedOption?.Localization.Should().BeSameAs(english);
        localizationService.CurrentLocalization.Should().BeSameAs(english);
        settingsStateService.SavedKey.Should().BeNull();
        settingsStateService.SavedValue.Should().BeNull();
    }

    [Fact]
    public void ClearSearchCommand_WithFilteredOptions_ShowsAllOptions()
    {
        LocalizationOption english = new(
            "English",
            new System.Globalization.CultureInfo("en-US"),
            true);
        LocalizationOption russian = new(
            "Русский",
            new System.Globalization.CultureInfo("ru-RU"),
            true);
        TestLocalizationService localizationService = new()
        {
            AvailableLocalizations = new List<LocalizationOption>
            {
                english,
                russian
            },
            CurrentLocalization = english
        };
        LanguageSettingViewModel viewModel = CreateViewModel(
            localizationService,
            new RecordingSettingsStateService());
        viewModel.SearchText = "Рус";

        viewModel.ClearSearchCommand.Execute(null);

        viewModel.SearchText.Should().BeEmpty();
        viewModel.Options.Should().OnlyContain(option => option.IsSearchMatch);
    }

    [Fact]
    public void RefreshLocalization_WithUnchangedOptions_PreservesCollectionAndSelection()
    {
        LocalizationOption english = new(
            "English",
            new System.Globalization.CultureInfo("en-US"),
            true);
        LocalizationOption russian = new(
            "Русский",
            new System.Globalization.CultureInfo("ru-RU"),
            true);
        TestLocalizationService localizationService = new()
        {
            AvailableLocalizations = new List<LocalizationOption>
            {
                english,
                russian
            },
            CurrentLocalization = russian
        };
        LanguageSettingViewModel viewModel = CreateViewModel(
            localizationService,
            new RecordingSettingsStateService());
        int collectionChangeCount = 0;
        INotifyCollectionChanged observableOptions = viewModel.Options;
        observableOptions.CollectionChanged += (_, _) => collectionChangeCount++;

        viewModel.RefreshLocalization();

        collectionChangeCount.Should().Be(0);
        viewModel.SelectedOption?.Localization.Should().BeSameAs(russian);
    }

    [Fact]
    public async Task SelectedOption_WithCustomVariant_ActivatesAndPersistsFilename()
    {
        LocalizationOption english = new(
            "English",
            new System.Globalization.CultureInfo("en-US"),
            true);
        LocalizationOption custom = new(
            "English concise",
            new System.Globalization.CultureInfo("en-US"),
            false);
        TestLocalizationService localizationService = new()
        {
            AvailableLocalizations = new List<LocalizationOption>
            {
                english,
                custom
            },
            CurrentLocalization = english
        };
        RecordingSettingsStateService settingsStateService = new();
        LanguageSettingViewModel viewModel = CreateViewModel(
            localizationService,
            settingsStateService);

        viewModel.SelectedOption = viewModel.Options.Single(option =>
            option.Localization == custom);
        Task? executionTask = viewModel.ApplyCommand.ExecutionTask;

        if (executionTask is not null)
        {
            await executionTask;
        }

        localizationService.CurrentLocalization.Should().BeSameAs(custom);
        settingsStateService.SavedKey.Should().Be(LanguageSettingDefinition.KeyValue);
        settingsStateService.SavedValue.Should().Be(custom.Id);
    }

    [Fact]
    public async Task RefreshOptionsCommand_WithFileDiscovered_UpdatesOptionsWithoutRestart()
    {
        LocalizationOption english = new(
            "English",
            new System.Globalization.CultureInfo("en-US"),
            true);
        LocalizationOption japanese = new(
            "日本語",
            new System.Globalization.CultureInfo("ja-JP"),
            false);
        TestLocalizationService localizationService = new()
        {
            AvailableLocalizations = new List<LocalizationOption>
            {
                english
            },
            CurrentLocalization = english
        };
        localizationService.RefreshAction = () =>
        {
            localizationService.AvailableLocalizations = new List<LocalizationOption>
            {
                english,
                japanese
            };
        };
        LanguageSettingViewModel viewModel = CreateViewModel(
            localizationService,
            new RecordingSettingsStateService());

        await viewModel.RefreshOptionsCommand.ExecuteAsync(null);

        viewModel.Options
            .Select(option => option.Localization)
            .Should()
            .Equal(english, japanese);
        viewModel.SelectedOption?.Localization.Should().BeSameAs(english);
    }

    private static LanguageSettingViewModel CreateViewModel(
        TestLocalizationService localizationService,
        ISettingsStateService settingsStateService)
    {
        return new LanguageSettingViewModel(
            new LanguageSettingDefinition(),
            localizationService,
            settingsStateService,
            new TestViewModelErrorHandler(),
            localizationService);
    }

    private sealed class RecordingSettingsStateService : ISettingsStateService
    {
        public string? SavedKey { get; private set; }
        public string? SavedValue { get; private set; }

        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
        }

        public Task<string?> LoadValueAsync(
            ISettingsDefinition definition,
            CancellationToken ct)
        {
            return Task.FromResult<string?>(null);
        }

        public Task SaveValueAsync(
            ISettingsDefinition definition,
            string value,
            CancellationToken ct)
        {
            SavedKey = definition.Key;
            SavedValue = value;

            return Task.CompletedTask;
        }
    }
}
