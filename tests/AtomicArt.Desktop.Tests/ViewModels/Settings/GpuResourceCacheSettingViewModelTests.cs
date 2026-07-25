using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class GpuResourceCacheSettingViewModelTests
{
    [Fact]
    public async Task SelectedOption_WhenChanged_SavesValue()
    {
        GpuResourceCacheOption firstOption = GpuResourceCacheSettingOptions.Options[0];
        GpuResourceCacheOption secondOption = GpuResourceCacheSettingOptions.Options[1];
        RecordingSettingsStateService settingsStateService = new();
        GpuResourceCacheSettingViewModel viewModel = new(
            new GpuResourceCacheSettingDefinition(),
            GpuResourceCacheSettingOptions.Options,
            firstOption,
            settingsStateService,
            new TestViewModelErrorHandler());

        viewModel.SelectedOption = secondOption;
        Task? executionTask = viewModel.SaveCommand.ExecutionTask;

        if (executionTask is not null)
        {
            await executionTask;
        }

        settingsStateService.SavedValue.Should().Be(secondOption.Value);
    }

    private sealed class RecordingSettingsStateService : ISettingsStateService
    {
        public string? SavedValue { get; private set; }

        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            throw new NotSupportedException("Applying settings is not used by this test.");
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
            throw new NotSupportedException("Applying a value is not used by this test.");
        }

        public Task<string?> LoadValueAsync(ISettingsDefinition definition, CancellationToken ct)
        {
            throw new NotSupportedException("Loading settings is not used by this test.");
        }

        public Task SaveValueAsync(
            ISettingsDefinition definition,
            string value,
            CancellationToken ct)
        {
            SavedValue = value;

            return Task.CompletedTask;
        }
    }
}
