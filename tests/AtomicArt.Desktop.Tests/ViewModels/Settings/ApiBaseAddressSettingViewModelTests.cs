using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class ApiBaseAddressSettingViewModelTests : DesktopControlTestBase
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveCommand_WithInvalidAddress_DoesNotApplyOrSaveValue(bool isCatalogLoading)
    {
        using ApiBaseAddressSettingTestContext context = new();
        context.ModelCatalog.SetLoading(isCatalogLoading);
        context.ViewModel.Value = "ftp://atomicart.test/";

        await context.ViewModel.SaveCommand.ExecuteAsync(null);

        context.EndpointService.BaseAddress.ToString().Should().Be("https://atomicart.test/");
        context.SettingsStateService.AppliedValue.Should().BeNull();
        context.SettingsStateService.SavedValue.Should().BeNull();
        context.ViewModel.ErrorMessage.Should().Be(TestLocalizationTextProvider.Default.Get(SettingsLocalizationKeys.ApiBaseAddress.Invalid));
    }

    [Fact]
    public async Task SaveCommand_WhileCatalogIsLoading_AppliesNewAddressWithoutWaiting()
    {
        using ApiBaseAddressSettingTestContext context = new();
        context.ModelCatalog.SetLoading(true);
        context.ViewModel.Value = "https://second.atomicart.test/";

        context.ViewModel.SaveCommand.CanExecute(null).Should().BeTrue();
        await context.ViewModel.SaveCommand.ExecuteAsync(null);

        context.EndpointService.BaseAddress.ToString().Should().Be("https://second.atomicart.test/");
        context.SettingsStateService.SavedValue.Should().Be("https://second.atomicart.test/");
        context.ViewModel.IsCatalogLoading.Should().BeTrue();
        context.ViewModel.HasErrorMessage.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_WhileSettingsAreBeingSaved_PreventsConcurrentSave()
    {
        using ApiBaseAddressSettingTestContext context = new();
        TaskCompletionSource saveCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.SettingsStateService.SaveTask = saveCompletion.Task;
        context.ViewModel.Value = "https://second.atomicart.test/";

        Task saveTask = context.ViewModel.SaveCommand.ExecuteAsync(null);

        try
        {
            context.ViewModel.IsLoading.Should().BeTrue();
            context.ViewModel.SaveCommand.CanExecute(null).Should().BeFalse();
        }
        finally
        {
            saveCompletion.SetResult();
            await saveTask;
        }
    }

    [Fact]
    public async Task View_WhileCatalogIsLoading_AllowsEditingAndSavingWithEnter()
    {
        await DispatchAsync(async () =>
        {
            using ApiBaseAddressSettingTestContext context = new();
            context.ModelCatalog.SetLoading(true);
            ApiBaseAddressSettingView view = new()
            {
                DataContext = context.ViewModel
            };
            Window window = Show(view);

            try
            {
                TextBox textBox = view.GetVisualDescendants().OfType<TextBox>().Single();
                textBox.IsEffectivelyEnabled.Should().BeTrue();
                textBox.Focus().Should().BeTrue();
                textBox.Text = "https://second.atomicart.test/";

                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                await (context.ViewModel.SaveCommand.ExecutionTask
                    ?? throw new InvalidOperationException("Address save was not started."));

                context.EndpointService.BaseAddress.ToString().Should().Be("https://second.atomicart.test/");
                context.SettingsStateService.SavedValue.Should().Be("https://second.atomicart.test/");
                context.ViewModel.IsCatalogLoading.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
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

    [Fact]
    public void SaveCommand_WithUnchangedAddress_CannotExecute()
    {
        using ApiBaseAddressSettingTestContext context = new();

        bool canExecute = context.ViewModel.SaveCommand.CanExecute(null);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void CatalogLoadingChanged_UpdatesBusyState()
    {
        using ApiBaseAddressSettingTestContext context = new();

        context.ModelCatalog.SetLoading(true);

        context.ViewModel.IsCatalogLoading.Should().BeTrue();
        context.ViewModel.IsBusy.Should().BeTrue();
        context.ViewModel.SaveCommand.CanExecute(null).Should().BeFalse();

        context.ModelCatalog.SetLoading(false);

        context.ViewModel.IsCatalogLoading.Should().BeFalse();
        context.ViewModel.IsBusy.Should().BeFalse();
    }

    private sealed class ApiBaseAddressSettingTestContext : IDisposable
    {
        public IApiEndpointService EndpointService { get; }
        public ImageModelOptionCatalog ModelCatalog { get; }
        public RecordingSettingsStateService SettingsStateService { get; }
        public ApiBaseAddressSettingViewModel ViewModel { get; }

        public ApiBaseAddressSettingTestContext()
        {
            EndpointService = TestApiEndpointServiceFactory.Create();
            ModelCatalog = new ImageModelOptionCatalog();
            SettingsStateService = new RecordingSettingsStateService(EndpointService);
            ViewModel = new ApiBaseAddressSettingViewModel(
                new ApiBaseAddressSettingDefinition(),
                EndpointService,
                ModelCatalog,
                new ImmediateUiThreadDispatcher(),
                SettingsStateService,
                new TestViewModelErrorHandler(),
                TestLocalizationTextProvider.Default);
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }

    private sealed class RecordingSettingsStateService : ISettingsStateService
    {
        public string? AppliedValue { get; private set; }
        public string? SavedValue { get; private set; }
        public Task SaveTask { get; set; } = Task.CompletedTask;

        private readonly IApiEndpointService _endpointService;

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
            return SaveTask;
        }
    }
}
