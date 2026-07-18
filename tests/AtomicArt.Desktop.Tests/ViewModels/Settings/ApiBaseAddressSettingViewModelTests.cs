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
        using ApiBaseAddressSettingTestContext context = new();
        context.ViewModel.Value = " https://second.atomicart.test/root ";

        await context.ViewModel.SaveCommand.ExecuteAsync(null);

        context.ViewModel.Value.Should().Be("https://second.atomicart.test/root/");
        context.EndpointService.BaseAddress.ToString().Should().Be(context.ViewModel.Value);
        context.SettingsStateService.AppliedValue.Should().Be(context.ViewModel.Value);
        context.SettingsStateService.SavedValue.Should().Be(context.ViewModel.Value);
        context.ViewModel.HasErrorMessage.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_WithInvalidAddress_DoesNotApplyOrSaveValue()
    {
        using ApiBaseAddressSettingTestContext context = new();
        context.ViewModel.Value = "ftp://atomicart.test/";

        await context.ViewModel.SaveCommand.ExecuteAsync(null);

        context.EndpointService.BaseAddress.ToString().Should().Be("https://atomicart.test/");
        context.SettingsStateService.AppliedValue.Should().BeNull();
        context.SettingsStateService.SavedValue.Should().BeNull();
        context.ViewModel.ErrorMessage.Should().Be(UiStrings.SettingsApiBaseAddressInvalid);
    }

    [Fact]
    public void BaseAddressChanged_WithRestoredAddress_SynchronizesDisplayedValue()
    {
        using ApiBaseAddressSettingTestContext context = new();
        ApiBaseAddress.TryCreate(
            "https://restored.atomicart.test/",
            out ApiBaseAddress? restoredAddress).Should().BeTrue();

        context.EndpointService.SetBaseAddress(restoredAddress
            ?? throw new InvalidOperationException("Restored address is required."));

        context.ViewModel.Value.Should().Be("https://restored.atomicart.test/");
    }

    private sealed class ApiBaseAddressSettingTestContext : IDisposable
    {
        public IApiEndpointService EndpointService { get; }
        public RecordingSettingsStateService SettingsStateService { get; }
        public ApiBaseAddressSettingViewModel ViewModel { get; }

        public ApiBaseAddressSettingTestContext()
        {
            EndpointService = TestApiEndpointServiceFactory.Create();
            SettingsStateService = new RecordingSettingsStateService(EndpointService);
            ViewModel = new ApiBaseAddressSettingViewModel(
                new ApiBaseAddressSettingDefinition(),
                EndpointService,
                new ImmediateUiThreadDispatcher(),
                SettingsStateService,
                new TestViewModelErrorHandler());
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
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
