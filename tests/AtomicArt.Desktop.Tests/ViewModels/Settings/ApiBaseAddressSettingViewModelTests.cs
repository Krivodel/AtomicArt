using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class ApiBaseAddressSettingViewModelTests
{
    [Fact]
    public async Task SaveCommand_WithValidAddress_AppliesAndSavesNormalizedValue()
    {
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create();
        RecordingSettingsStateService settingsStateService = new(endpointService);
        using ApiBaseAddressSettingViewModel viewModel = CreateViewModel(
            endpointService,
            settingsStateService);
        viewModel.Value = " https://second.atomicart.test/root ";

        await viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.Value.Should().Be("https://second.atomicart.test/root/");
        endpointService.BaseAddress.ToString().Should().Be(viewModel.Value);
        settingsStateService.AppliedValue.Should().Be(viewModel.Value);
        settingsStateService.SavedValue.Should().Be(viewModel.Value);
        viewModel.HasErrorMessage.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_WithInvalidAddress_DoesNotApplyOrSaveValue()
    {
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create();
        RecordingSettingsStateService settingsStateService = new(endpointService);
        using ApiBaseAddressSettingViewModel viewModel = CreateViewModel(
            endpointService,
            settingsStateService);
        viewModel.Value = "ftp://atomicart.test/";

        await viewModel.SaveCommand.ExecuteAsync(null);

        endpointService.BaseAddress.ToString().Should().Be("https://atomicart.test/");
        settingsStateService.AppliedValue.Should().BeNull();
        settingsStateService.SavedValue.Should().BeNull();
        viewModel.ErrorMessage.Should().Be(UiStrings.SettingsApiBaseAddressInvalid);
    }

    [Fact]
    public void BaseAddressChanged_WithRestoredAddress_SynchronizesDisplayedValue()
    {
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create();
        RecordingSettingsStateService settingsStateService = new(endpointService);
        using ApiBaseAddressSettingViewModel viewModel = CreateViewModel(
            endpointService,
            settingsStateService);
        ApiBaseAddress.TryCreate(
            "https://restored.atomicart.test/",
            out ApiBaseAddress? restoredAddress).Should().BeTrue();

        endpointService.SetBaseAddress(restoredAddress
            ?? throw new InvalidOperationException("Restored address is required."));

        viewModel.Value.Should().Be("https://restored.atomicart.test/");
    }

    private static ApiBaseAddressSettingViewModel CreateViewModel(
        IApiEndpointService endpointService,
        ISettingsStateService settingsStateService)
    {
        return new ApiBaseAddressSettingViewModel(
            new ApiBaseAddressSettingDefinition(),
            endpointService,
            new ImmediateUiThreadDispatcher(),
            settingsStateService,
            new TestViewModelErrorHandler());
    }

    private sealed class RecordingSettingsStateService : ISettingsStateService
    {
        private readonly IApiEndpointService _endpointService;

        public string? AppliedValue { get; private set; }
        public string? SavedValue { get; private set; }

        public RecordingSettingsStateService(IApiEndpointService endpointService)
        {
            _endpointService = endpointService
                ?? throw new ArgumentNullException(nameof(endpointService));
        }

        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            throw new NotSupportedException("Applying all settings is not used by this test.");
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
            AppliedValue = value;

            if (ApiBaseAddress.TryCreate(value, out ApiBaseAddress? baseAddress)
                && baseAddress is not null)
            {
                _endpointService.SetBaseAddress(baseAddress);
            }
        }

        public Task<string?> LoadValueAsync(ISettingsDefinition definition, CancellationToken ct)
        {
            throw new NotSupportedException("Loading a setting is not used by this test.");
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
