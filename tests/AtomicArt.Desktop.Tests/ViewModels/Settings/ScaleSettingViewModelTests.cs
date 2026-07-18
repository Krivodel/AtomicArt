using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class ScaleSettingViewModelTests
{
    [Fact]
    public async Task ApplyCommand_WithSelectedOption_SavesValueByDefinitionKey()
    {
        UiScaleSettingDefinition definition = new();
        UiScaleOption option = new UiScale125OptionDefinition().Option;
        RecordingSettingsStateService settingsStateService = new();
        IUiScaleSettingValueConverter valueConverter = new UiScaleSettingValueConverter();
        ScaleSettingViewModel viewModel = new(
            definition,
            [option],
            option,
            settingsStateService,
            valueConverter,
            new TestViewModelErrorHandler());

        await viewModel.ApplyCommand.ExecuteAsync(null);

        settingsStateService.AppliedKey.Should().Be(definition.Key);
        settingsStateService.AppliedValue.Should().Be(valueConverter.Format(option.Value));
        settingsStateService.SavedKey.Should().Be(definition.Key);
        settingsStateService.SavedValue.Should().Be(valueConverter.Format(option.Value));
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
