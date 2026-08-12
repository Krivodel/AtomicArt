using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class BooleanSettingViewModelTests
{
    [Fact]
    public async Task IsChecked_WhenChanged_AppliesAndSavesValue()
    {
        ConfirmDeletionSettingDefinition definition = new();
        RecordingBooleanSettingValueSource valueSource = new(
            definition.DefaultValue);
        RecordingSettingsStateService settingsStateService = new();
        IBooleanSettingValueConverter valueConverter =
            new BooleanSettingValueConverter();
        using BooleanSettingViewModel viewModel = new(
            definition,
            valueSource,
            settingsStateService,
            valueConverter,
            new TestViewModelErrorHandler(),
            TestLocalizationTextProvider.Default);

        viewModel.IsChecked = !definition.DefaultValue;
        Task? executionTask = viewModel.ApplyCommand.ExecutionTask;

        if (executionTask is not null)
        {
            await executionTask;
        }

        string expectedValue = valueConverter.Format(!definition.DefaultValue);
        settingsStateService.AppliedKey.Should().Be(definition.Key);
        settingsStateService.AppliedValue.Should().Be(expectedValue);
        settingsStateService.SavedKey.Should().Be(definition.Key);
        settingsStateService.SavedValue.Should().Be(expectedValue);
    }

    [Fact]
    public void ValueSource_WhenChanged_UpdatesValueWithoutSaving()
    {
        ConfirmDeletionSettingDefinition definition = new();
        RecordingBooleanSettingValueSource valueSource = new(
            definition.DefaultValue);
        RecordingSettingsStateService settingsStateService = new();
        using BooleanSettingViewModel viewModel = new(
            definition,
            valueSource,
            settingsStateService,
            new BooleanSettingValueConverter(),
            new TestViewModelErrorHandler(),
            TestLocalizationTextProvider.Default);

        valueSource.SetValue(!definition.DefaultValue);

        viewModel.IsChecked.Should().Be(!definition.DefaultValue);
        settingsStateService.SavedValue.Should().BeNull();
    }

    [Fact]
    public void Dispose_WhenValueSourceChanges_KeepsCurrentValue()
    {
        ConfirmDeletionSettingDefinition definition = new();
        RecordingBooleanSettingValueSource valueSource = new(
            definition.DefaultValue);
        BooleanSettingViewModel viewModel = new(
            definition,
            valueSource,
            new RecordingSettingsStateService(),
            new BooleanSettingValueConverter(),
            new TestViewModelErrorHandler(),
            TestLocalizationTextProvider.Default);

        viewModel.Dispose();
        valueSource.SetValue(!definition.DefaultValue);

        viewModel.IsChecked.Should().Be(definition.DefaultValue);
    }

    private sealed class RecordingBooleanSettingValueSource :
        IBooleanSettingValueSource
    {
        public bool CurrentValue { get; private set; }

        public event EventHandler? ValueChanged;

        public RecordingBooleanSettingValueSource(bool currentValue)
        {
            CurrentValue = currentValue;
        }

        public void SetValue(bool value)
        {
            CurrentValue = value;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class RecordingSettingsStateService : ISettingsStateService
    {
        public string? AppliedKey { get; private set; }
        public string? AppliedValue { get; private set; }
        public string? SavedKey { get; private set; }
        public string? SavedValue { get; private set; }

        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            throw new NotSupportedException(
                "Applying saved settings is not used by this test.");
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
            AppliedKey = definition.Key;
            AppliedValue = value;
        }

        public Task<string?> LoadValueAsync(
            ISettingsDefinition definition,
            CancellationToken ct)
        {
            throw new NotSupportedException(
                "Loading settings is not used by this test.");
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
