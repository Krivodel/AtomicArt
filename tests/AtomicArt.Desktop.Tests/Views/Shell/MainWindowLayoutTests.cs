using Microsoft.Extensions.DependencyInjection;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using SukiUI.Controls;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Updates;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.ViewModels.Dialogs;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views;
using AtomicArt.Desktop.Views.Dialogs;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Generation;
using AtomicArt.Desktop.Views.Shell;

namespace AtomicArt.Desktop.Tests.Views.Shell;

public sealed class MainWindowLayoutTests : AnimatedGalleryControlTestBase
{
    private const int GalleryRowIndex = 0;
    private const int GenerationPanelRowIndex = 1;
    private const int ExpectedShellRowCount = 2;
    private const double HeightTolerance = 0.1d;
    private const string GenerationPanelResizeGripName = "GenerationPanelResizeGrip";

    [Fact]
    public async Task MainWindow_WhenUpdateIsAvailable_ShowsSukiToastActions()
    {
        await DispatchAsync(async () =>
        {
            Mock<IApplicationUpdateService> updateServiceMock = new();
            updateServiceMock
                .SetupGet(service => service.CanCheckForUpdates)
                .Returns(true);
            updateServiceMock
                .Setup(service => service.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApplicationUpdate("1.2.3"));

            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton(updateServiceMock.Object);
            });
            MainWindow window = context.Window;

            window.Show();

            MainWindowViewModel viewModel = window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;
            await viewModel.ApplicationUpdate.StartMonitoringCommand.ExecuteAsync(null);
            window.CaptureRenderedFrame();

            SukiToast toast = window
                .GetVisualDescendants()
                .OfType<SukiToast>()
                .Single();
            Button[] actionButtons = toast
                .GetVisualDescendants()
                .OfType<Button>()
                .ToArray();

            actionButtons
                .Select(button => button.Content)
                .Should()
                .Equal(UiStrings.UpdateLater, UiStrings.UpdateInstall);
        });
    }

    [Fact]
    public void MainWindow_WhenShown_ConfiguresGenerationPanelResizeGrip()
    {
        Dispatch(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;

            window.Show();
            window.CaptureRenderedFrame();

            GridSplitter resizeGrip = window
                .GetVisualDescendants()
                .OfType<GridSplitter>()
                .Single(splitter => splitter.Name == GenerationPanelResizeGripName);
            Grid shellContentGrid = resizeGrip.Parent
                .Should()
                .BeOfType<Grid>()
                .Subject;
            RowDefinitions rowDefinitions = shellContentGrid.RowDefinitions;

            Grid.GetRow(resizeGrip).Should().Be(GenerationPanelRowIndex);
            resizeGrip.ResizeDirection.Should().Be(GridResizeDirection.Rows);
            resizeGrip.ResizeBehavior.Should().Be(GridResizeBehavior.PreviousAndCurrent);
            rowDefinitions.Should().HaveCount(ExpectedShellRowCount);
            rowDefinitions[GalleryRowIndex].MinHeight.Should().Be(0d);
            rowDefinitions[GenerationPanelRowIndex].MinHeight.Should().BeGreaterThan(0d);
            rowDefinitions[GenerationPanelRowIndex].MinHeight.Should().BeApproximately(
                rowDefinitions[GenerationPanelRowIndex].ActualHeight,
                HeightTolerance);
        });
    }

    [Fact]
    public void MainWindow_WhenErrorIsShown_DisplaysSharedModalOverlay()
    {
        Dispatch(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;
            MainWindowViewModel viewModel = window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;
            window.Show();

            context.DialogService.ShowError(UiStrings.GenerationApiUnavailable);
            window.CaptureRenderedFrame();

            ModalOverlayPresenterControl presenter = window
                .GetVisualDescendants()
                .OfType<ModalOverlayPresenterControl>()
                .Single(overlay => ReferenceEquals(
                    overlay.Body,
                    viewModel.ErrorDialog));

            presenter.IsOpen.Should().BeTrue();
            presenter
                .GetVisualDescendants()
                .OfType<ErrorDialogOverlayView>()
                .Should()
                .ContainSingle();
            presenter
                .GetVisualDescendants()
                .OfType<ModalOverlayControl>()
                .Should()
                .ContainSingle();
        });
    }

    private static void RegisterViewTemplates(IServiceProvider serviceProvider)
    {
        Avalonia.Application.Current?.DataTemplates.Add(
            new ViewModelViewTemplate(
            [
                new ViewTemplateRegistration(
                    typeof(GalleryViewModel),
                    serviceProvider.GetRequiredService<GalleryView>),
                new ViewTemplateRegistration(
                    typeof(IModelPanelViewModel),
                    serviceProvider.GetRequiredService<GenerationPanelView>),
                new ViewTemplateRegistration(
                    typeof(ErrorDialogViewModel),
                    serviceProvider.GetRequiredService<ErrorDialogOverlayView>)
            ]));
    }

    private sealed class MainWindowTestContext : IDisposable
    {
        public MainWindow Window { get; }
        public IDialogService DialogService { get; }

        private readonly ServiceProvider _serviceProvider;

        public MainWindowTestContext(Action<ServiceCollection>? configureServices = null)
        {
            ServiceCollection services = new();
            services.AddSingleton(TestApiConfiguration.Create());
            services.AddDesktopServices();
            configureServices?.Invoke(services);

            _serviceProvider = services.BuildServiceProvider();
            RegisterViewTemplates(_serviceProvider);
            DialogService = _serviceProvider.GetRequiredService<IDialogService>();
            Window = _serviceProvider.GetRequiredService<MainWindow>();
        }

        public void Dispose()
        {
            Window.Close();
            _serviceProvider.Dispose();
        }
    }
}
