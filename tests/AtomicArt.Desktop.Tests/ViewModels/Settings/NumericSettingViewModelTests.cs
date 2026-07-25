using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class NumericSettingViewModelTests
{
    [Fact]
    public async Task ApplyCommand_WithSelectedOption_SavesValueByDefinitionKey()
    {
        UiScaleSettingDefinition definition = new();
        UiScaleOption option = new UiScale125OptionDefinition().Option;
        RecordingNumericSettingValueSource valueSource = new(option.Value);
        RecordingSettingsStateService settingsStateService = new();
        IDoubleSettingValueConverter valueConverter = new DoubleSettingValueConverter();
        using NumericSettingViewModel viewModel = new(
            definition,
            [option],
            valueSource,
            settingsStateService,
            valueConverter,
            new TestViewModelErrorHandler());

        await viewModel.ApplyCommand.ExecuteAsync(null);

        settingsStateService.AppliedKey.Should().Be(definition.Key);
        settingsStateService.AppliedValue.Should().Be(valueConverter.Format(option.Value));
        settingsStateService.SavedKey.Should().Be(definition.Key);
        settingsStateService.SavedValue.Should().Be(valueConverter.Format(option.Value));
    }

    [Fact]
    public void SelectedOption_WhenValueSourceChanges_UsesCurrentOption()
    {
        UiScaleOption firstOption = new UiScale100OptionDefinition().Option;
        UiScaleOption secondOption = new UiScale125OptionDefinition().Option;
        RecordingNumericSettingValueSource valueSource = new(firstOption.Value);
        using NumericSettingViewModel viewModel = new(
            new UiScaleSettingDefinition(),
            [firstOption, secondOption],
            valueSource,
            new RecordingSettingsStateService(),
            new DoubleSettingValueConverter(),
            new TestViewModelErrorHandler());

        valueSource.SetValue(secondOption.Value);

        viewModel.SelectedOption.Should().Be(secondOption);
    }

    [Fact]
    public void Dispose_WhenValueSourceChanges_KeepsSelectedOption()
    {
        UiScaleOption firstOption = new UiScale100OptionDefinition().Option;
        UiScaleOption secondOption = new UiScale125OptionDefinition().Option;
        RecordingNumericSettingValueSource valueSource = new(firstOption.Value);
        NumericSettingViewModel viewModel = new(
            new UiScaleSettingDefinition(),
            [firstOption, secondOption],
            valueSource,
            new RecordingSettingsStateService(),
            new DoubleSettingValueConverter(),
            new TestViewModelErrorHandler());

        viewModel.Dispose();
        valueSource.SetValue(secondOption.Value);

        viewModel.SelectedOption.Should().Be(firstOption);
    }

    private sealed class RecordingNumericSettingValueSource : INumericSettingValueSource
    {
        public double CurrentValue { get; private set; }

        public event EventHandler? ValueChanged;

        public RecordingNumericSettingValueSource(double currentValue)
        {
            CurrentValue = currentValue;
        }

        public void SetValue(double value)
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
            throw new NotSupportedException("Applying settings is not used by this test.");
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
            AppliedKey = definition.Key;
            AppliedValue = value;
        }

        public Task<string?> LoadValueAsync(ISettingsDefinition definition, CancellationToken ct)
        {
            throw new NotSupportedException("Loading settings is not used by this test.");
        }

        public Task SaveValueAsync(ISettingsDefinition definition, string value, CancellationToken ct)
        {
            SavedKey = definition.Key;
            SavedValue = value;

            return Task.CompletedTask;
        }
    }
}
