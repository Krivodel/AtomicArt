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
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Updates;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.TestDoubles;
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
    private const double PositionTolerance = 0.1d;
    private const double UiScale = 1.5d;
    private const string GenerationPanelHostName = "GenerationPanelHost";
    private const string GenerationPanelResizeGripName = "GenerationPanelResizeGrip";
    private const string GenerationPanelWidthResourceKey = "GenerationPanelWidth";
    private const string ShellContentGridName = "ShellContentGrid";

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
                .Equal(
                    context.TextProvider.Get(UpdateLocalizationKeys.Actions.Later),
                    context.TextProvider.Get(UpdateLocalizationKeys.Actions.Install));
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
    public void GenerationPanel_WhenScaledWindowNarrows_ShrinksFromPreferredWidthAndRemainsContained()
    {
        Dispatch(() =>
        {
            RecordingUiScaleService uiScaleService = new(UiScale);
            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton<IUiScaleService>(uiScaleService);
            });
            MainWindow window = context.Window;
            window.Show();
            window.CaptureRenderedFrame();
            Border generationPanel = window
                .GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == GenerationPanelHostName);
            Grid shellContentGrid = window
                .GetVisualDescendants()
                .OfType<Grid>()
                .Single(grid => grid.Name == ShellContentGridName);
            double preferredWidth = GetDoubleResource(
                generationPanel,
                GenerationPanelWidthResourceKey);

            generationPanel.Bounds.Width.Should().Be(preferredWidth);

            window.Width = window.MinWidth;
            window.CaptureRenderedFrame();

            generationPanel.Bounds.Width.Should().BeLessThan(preferredWidth);
            generationPanel.Bounds.Left.Should().BeGreaterThanOrEqualTo(0d);
            generationPanel.Bounds.Right.Should().BeLessThanOrEqualTo(
                shellContentGrid.Bounds.Width);
            generationPanel.Bounds.Center.X.Should().BeApproximately(
                shellContentGrid.Bounds.Width / 2d,
                PositionTolerance);
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

            context.DialogService.ShowError(
                context.TextProvider.Get(
                    GenerationUiLocalizationKeys.Errors.ApiUnavailable));
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

    private static double GetDoubleResource(Control control, string resourceKey)
    {
        if (control.TryFindResource(resourceKey, out object? value)
            && (value is double doubleValue))
        {
            return doubleValue;
        }

        throw new InvalidOperationException($"Double resource '{resourceKey}' was not found.");
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
        public ILocalizationTextProvider TextProvider { get; }

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
            TextProvider =
                _serviceProvider.GetRequiredService<ILocalizationTextProvider>();
            Window = _serviceProvider.GetRequiredService<MainWindow>();
        }

        public void Dispose()
        {
            Window.Close();
            _serviceProvider.Dispose();
        }
    }
}
