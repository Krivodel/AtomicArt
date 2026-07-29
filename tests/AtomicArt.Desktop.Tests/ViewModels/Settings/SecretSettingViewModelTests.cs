using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class SecretSettingViewModelTests
{
    [Fact]
    public async Task LoadCommand_WithStoredValue_DisplaysValueWithoutPendingSave()
    {
        RecordingSecretStore secretStore = new()
        {
            StoredValue = "stored-value-for-test-only"
        };
        SecretSettingViewModel viewModel = new(
            new GoogleApiKeySettingDefinition(),
            secretStore,
            new TestViewModelErrorHandler(),
            TestLocalizationTextProvider.Default);

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.Value.Should().Be("stored-value-for-test-only");
        viewModel.LoadCommand.CanExecute(null).Should().BeFalse();
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_AfterSuccessfulSave_KeepsValueAndDisablesRepeatedSave()
    {
        SecretSettingViewModel viewModel = new(
            new GoogleApiKeySettingDefinition(),
            new RecordingSecretStore(),
            new TestViewModelErrorHandler(),
            TestLocalizationTextProvider.Default)
        {
            Value = "value-for-test-only"
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.Value.Should().Be("value-for-test-only");
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_WhenPendingValueIsCleared_SavesEmptyValue()
    {
        RecordingSecretStore secretStore = new();
        SecretSettingViewModel viewModel = new(
            new GoogleApiKeySettingDefinition(),
            secretStore,
            new TestViewModelErrorHandler(),
            TestLocalizationTextProvider.Default)
        {
            Value = "value-for-test-only"
        };
        viewModel.Value = string.Empty;

        await viewModel.SaveCommand.ExecuteAsync(null);

        secretStore.StoredValue.Should().BeEmpty();
    }

    private sealed class RecordingSecretStore : ISecretStore
    {
        public string? StoredValue { get; set; }

        public Task<string?> GetSecretAsync(string key, CancellationToken ct)
        {
            return Task.FromResult(StoredValue);
        }

        public Task SetSecretAsync(string key, string value, CancellationToken ct)
        {
            StoredValue = value;

            return Task.CompletedTask;
        }
    }
}
