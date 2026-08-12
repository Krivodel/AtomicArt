using Microsoft.Extensions.DependencyInjection;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using SukiUI.Controls;
using SukiUI.Controls.GlassMorphism;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Updates;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Generation;
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
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Tests.Views.Shell;

public sealed class MainWindowLayoutTests : AnimatedGalleryControlTestBase
{
    private const int GalleryRowIndex = 0;
    private const int GenerationPanelRowIndex = 1;
    private const int ExpectedShellRowCount = 2;
    private const double HeightTolerance = 0.1d;
    private const double InitialUiScale = 0.6d;
    private const double PositionTolerance = 0.1d;
    private const double UiScale = 1.5d;
    private const string ConfirmationDialogHostName = "ConfirmationDialogHost";
    private const string GenerationPanelHostName = "GenerationPanelHost";
    private const string GenerationPanelResizeGripName = "GenerationPanelResizeGrip";
    private const string GenerationPanelWidthResourceKey = "GenerationPanelWidth";
    private const string ShellContentGridName = "ShellContentGrid";
    private const string TitleBarName = "PART_TitleBar";
    private const string UpdateToastHostName = "UpdateToastHost";

    private static readonly RelativePoint BottomRightOrigin = new(
        1d,
        1d,
        RelativeUnit.Relative);
    private static readonly TimeSpan DialogCompletionTimeout =
        TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ConfirmationDialogTransitionDuration =
        TimeSpan.FromMilliseconds(200d);
    private static readonly TimeSpan ConfirmationDialogOpacityTransitionDuration =
        TimeSpan.FromMilliseconds(100d);

    [Fact]
    public async Task MainWindow_WhenUpdateIsAvailable_ShowsSukiToastActions()
    {
        await DispatchAsync(async () =>
        {
            Mock<IApplicationUpdateService> updateServiceMock =
                CreateAvailableUpdateServiceMock();

            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton(updateServiceMock.Object);
            });
            MainWindow window = context.Window;

            window.Show();
            await ShowAvailableUpdateAsync(window);

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
                    context.TextProvider.Get(CommonLocalizationKeys.NotNow),
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
    public async Task MainWindow_WhenUpdateToastIsShown_FollowsUiScaleAndKeepsBottomRightAnchor()
    {
        await DispatchAsync(async () =>
        {
            Mock<IApplicationUpdateService> updateServiceMock =
                CreateAvailableUpdateServiceMock();
            RecordingUiScaleService uiScaleService = new(InitialUiScale);
            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton(updateServiceMock.Object);
                services.AddSingleton<IUiScaleService>(uiScaleService);
            });
            MainWindow window = context.Window;
            window.Show();
            await ShowAvailableUpdateAsync(window);

            SukiToastHost toastHost = window
                .GetVisualDescendants()
                .OfType<SukiToastHost>()
                .Single(host => host.Name == UpdateToastHostName);
            ScaleTransform scaleTransform = toastHost.RenderTransform
                .Should()
                .BeOfType<ScaleTransform>()
                .Subject;
            scaleTransform.ScaleX.Should().Be(InitialUiScale);
            scaleTransform.ScaleY.Should().Be(InitialUiScale);
            Point anchorBeforeScaleChange = toastHost.TranslatePoint(
                new Point(toastHost.Bounds.Width, toastHost.Bounds.Height),
                window)
                ?? throw new InvalidOperationException(
                    "The update toast anchor position is unavailable.");

            uiScaleService.SetScale(UiScale);
            window.CaptureRenderedFrame();

            Point anchorAfterScaleChange = toastHost.TranslatePoint(
                new Point(toastHost.Bounds.Width, toastHost.Bounds.Height),
                window)
                ?? throw new InvalidOperationException(
                    "The updated toast anchor position is unavailable.");
            scaleTransform.ScaleX.Should().Be(UiScale);
            scaleTransform.ScaleY.Should().Be(UiScale);
            toastHost.RenderTransformOrigin.Should().Be(BottomRightOrigin);
            anchorAfterScaleChange.X.Should().BeApproximately(
                anchorBeforeScaleChange.X,
                PositionTolerance);
            anchorAfterScaleChange.Y.Should().BeApproximately(
                anchorBeforeScaleChange.Y,
                PositionTolerance);
        });
    }

    [Fact]
    public void MainWindow_WhenShown_KeepsConfirmationHostBelowTitleBar()
    {
        Dispatch(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;

            window.Show();
            window.CaptureRenderedFrame();

            SukiDialogHost dialogHost = window
                .GetVisualDescendants()
                .OfType<SukiDialogHost>()
                .Single(host => host.Name == ConfirmationDialogHostName);
            Control titleBar = window
                .GetVisualDescendants()
                .OfType<Control>()
                .Single(control => control.Name == TitleBarName);
            Point dialogHostPosition = dialogHost
                .TranslatePoint(default, window)
                ?? throw new InvalidOperationException(
                    "The confirmation dialog host position is unavailable.");
            Point titleBarPosition = titleBar
                .TranslatePoint(default, window)
                ?? throw new InvalidOperationException(
                    "The title bar position is unavailable.");

            dialogHostPosition.Y.Should().BeGreaterThanOrEqualTo(
                titleBarPosition.Y + titleBar.Bounds.Height);
        });
    }

    [Fact]
    public void MainWindow_WhenShown_ConfiguresFasterConfirmationDialogTransitions()
    {
        Dispatch(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;

            window.Show();
            window.CaptureRenderedFrame();

            SukiDialogHost dialogHost = window
                .GetVisualDescendants()
                .OfType<SukiDialogHost>()
                .Single(host => host.Name == ConfirmationDialogHostName);
            ContentControl dialogContent = dialogHost
                .GetVisualDescendants()
                .OfType<ContentControl>()
                .Single(control => control.Name == "PART_DialogContent");
            Transitions transitions = dialogContent.Transitions
                ?? throw new InvalidOperationException(
                    "The confirmation dialog transitions were not found.");
            ThicknessTransition movementTransition = transitions
                .OfType<ThicknessTransition>()
                .Single();
            DoubleTransition opacityTransition = transitions
                .OfType<DoubleTransition>()
                .Single();
            TransformOperationsTransition transformTransition = transitions
                .OfType<TransformOperationsTransition>()
                .Single();

            movementTransition.Duration.Should().Be(
                ConfirmationDialogTransitionDuration);
            opacityTransition.Duration.Should().Be(
                ConfirmationDialogOpacityTransitionDuration);
            transformTransition.Duration.Should().Be(
                ConfirmationDialogTransitionDuration);
        });
    }

    [Fact]
    public async Task Confirmation_WhenOpen_UsesOpaquePopupBackgroundWithoutBlur()
    {
        await DispatchAsync(async () =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;
            window.Show();
            Task<bool> resultTask = context.DialogService.ShowConfirmationAsync(
                CreateDeletionConfirmationRequest(),
                CancellationToken.None);
            window.CaptureRenderedFrame();

            SukiDialog dialog = window
                .GetVisualDescendants()
                .OfType<SukiDialog>()
                .Single();
            SukiBlurBackground blurBackground = dialog
                .GetVisualDescendants()
                .OfType<SukiBlurBackground>()
                .Single();
            Border[] cardSurfaces = dialog
                .GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Classes.Contains("GlassCardBorderPartCard"))
                .ToArray();
            Color popupBackground = GetColorResource(
                dialog,
                "SukiPopupBackground");

            dialog.Classes.Should().Contain("confirmation-dialog");
            blurBackground.IsVisible.Should().BeFalse();
            cardSurfaces.Should().NotBeEmpty();

            foreach (Border cardSurface in cardSurfaces)
            {
                cardSurface.Opacity.Should().Be(1d);
                ISolidColorBrush backgroundBrush = cardSurface.Background
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Subject;
                backgroundBrush.Color.Should().Be(popupBackground);
            }

            Button cancelButton = dialog.ActionButtons
                .OfType<Button>()
                .First();
            cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bool result = await resultTask.WaitAsync(DialogCompletionTimeout);

            result.Should().BeFalse();
        });
    }

    [Fact]
    public async Task Escape_WhenConfirmationIsOpen_ClosesDialogWithoutExitingSelectionMode()
    {
        await DispatchAsync(async () =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;
            window.Show();
            MainWindowViewModel viewModel = window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;
            viewModel.Gallery.AddGeneratedItems(
                [GenerationItemDtoTestFactory.Create()],
                0);
            GenerationItemViewModel item = viewModel.Gallery.Items.Single();
            viewModel.Gallery.ToggleSelectionCommand.Execute(item);
            Task<bool> resultTask = context.DialogService.ShowConfirmationAsync(
                CreateDeletionConfirmationRequest(),
                CancellationToken.None);
            window.CaptureRenderedFrame();
            KeyEventArgs keyEventArgs = new()
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
                PhysicalKey = PhysicalKey.Escape
            };

            window.RaiseEvent(keyEventArgs);
            bool result = await resultTask.WaitAsync(DialogCompletionTimeout);

            result.Should().BeFalse();
            viewModel.Gallery.IsSelectionMode.Should().BeTrue();
            item.IsSelected.Should().BeTrue();
        });
    }

    [Fact]
    public void MainWindow_WhenShown_ProvidesPanelBoundaryForAttachmentDrag()
    {
        Dispatch(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;

            window.Show();
            window.CaptureRenderedFrame();

            Border generationPanelHost = window
                .FindControl<Border>(GenerationPanelHostName)
                ?? throw new InvalidOperationException(
                    "Generation panel host was not found.");
            AnimatedAttachmentListControl attachmentList = window
                .GetVisualDescendants()
                .OfType<AnimatedAttachmentListControl>()
                .Single();

            AttachmentImageDragBehavior
                .GetDragBoundary(attachmentList)
                .Should()
                .BeSameAs(generationPanelHost);
        });
    }

    [Fact]
    public async Task Settings_WhenClosed_RestoresPromptInputFocus()
    {
        await DispatchAsync(async () =>
        {
            ImageModelOptionCatalog modelCatalog = new();
            modelCatalog.Initialize(ApiModelMetadataTestCatalog.LoadCatalog());
            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton<IImageModelOptionCatalog>(modelCatalog);
            });
            MainWindow window = context.Window;
            MainWindowViewModel viewModel = window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;

            window.Show();
            await window.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Background);

            TextBox promptInput = window
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single(textBox => string.Equals(
                    textBox.Name,
                    "PromptTextBox",
                    StringComparison.Ordinal));
            Button settingsButton = window
                .GetVisualDescendants()
                .OfType<Button>()
                .Single(button => ReferenceEquals(
                    button.Command,
                    viewModel.OpenSettingsCommand));

            promptInput.IsFocused.Should().BeTrue();
            settingsButton.Focus().Should().BeTrue();
            viewModel.OpenSettingsCommand.Execute(null);
            promptInput.IsFocused.Should().BeFalse();

            viewModel.Settings.CloseCommand.Execute(null);
            await window.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Background);

            promptInput.IsFocused.Should().BeTrue();
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

    private static Mock<IApplicationUpdateService> CreateAvailableUpdateServiceMock()
    {
        Mock<IApplicationUpdateService> updateServiceMock = new();
        updateServiceMock
            .SetupGet(service => service.CanCheckForUpdates)
            .Returns(true);
        updateServiceMock
            .Setup(service => service.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationUpdate("1.2.3"));

        return updateServiceMock;
    }

    private static async Task ShowAvailableUpdateAsync(MainWindow window)
    {
        MainWindowViewModel viewModel = window.DataContext
            .Should()
            .BeOfType<MainWindowViewModel>()
            .Subject;

        await viewModel.ApplicationUpdate.StartMonitoringCommand.ExecuteAsync(null);
        window.CaptureRenderedFrame();
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

    private static Color GetColorResource(Control control, string resourceKey)
    {
        if (control.TryFindResource(resourceKey, out object? value)
            && (value is Color color))
        {
            return color;
        }

        throw new InvalidOperationException($"Color resource '{resourceKey}' was not found.");
    }

    private static LocalizedConfirmationDialogRequest CreateDeletionConfirmationRequest()
    {
        object?[] messageArguments = [1];

        return new LocalizedConfirmationDialogRequest(
            GalleryLocalizationKeys.DeletionConfirmationTitle,
            GalleryLocalizationKeys.DeletionConfirmationMessage,
            GalleryLocalizationKeys.ConfirmDeletion,
            CommonLocalizationKeys.Cancel,
            ConfirmationDialogKind.Destructive,
            ConfirmationDialogBackgroundClickBehavior.Dismiss,
            messageArguments);
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
