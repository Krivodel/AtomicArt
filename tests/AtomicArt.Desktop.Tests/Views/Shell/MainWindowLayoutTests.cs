using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using SukiUI.Controls;
using SukiUI.Controls.GlassMorphism;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.OpenRouter;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Services.Updates;
using AtomicArt.Desktop.Services.Windowing;
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
using AtomicArt.Desktop.Views.Dlss5;
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
    private const string LeftWidthResizeGripName = "GenerationPanelLeftWidthResizeGrip";
    private const string RightWidthResizeGripName = "GenerationPanelWidthResizeGrip";
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
    public async Task MainWindow_WithAccountBalance_ShowsTextBeforeAllTitleButtons()
    {
        await DispatchAsync(async () =>
        {
            Mock<IOpenRouterBalanceApiClient> balanceClient = new();
            balanceClient.Setup(client => client.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1234m);
            Mock<ISecretStore> store = new();
            store.Setup(service => service.GetSecretAsync(OpenRouterManagementKeySettingDefinition.SecretNameValue,
                It.IsAny<CancellationToken>())).ReturnsAsync("management-key");
            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton(balanceClient.Object);
                services.AddSingleton(store.Object);
            });
            MainWindow window = context.Window;
            MainWindowViewModel viewModel = window.DataContext.Should().BeOfType<MainWindowViewModel>().Subject;
            await viewModel.OpenRouterBalance.RefreshAsync(CancellationToken.None);

            window.Show();
            window.CaptureRenderedFrame();

            TextBlock text = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(control => control.Classes.Contains("account-balance"));
            text.IsVisible.Should().BeTrue();
            text.Text.Should().Be(context.TextProvider.Format(ShellLocalizationKeys.OpenRouterBalanceFormat, "1234"));
            double balanceRight = (text.TranslatePoint(default, window)
                ?? throw new InvalidOperationException("Balance text is not attached.")).X + text.Bounds.Width;
            Control titleBar = window.GetVisualDescendants().OfType<Control>()
                .Single(control => control.Name == TitleBarName);
            foreach (Button button in titleBar.GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible))
            {
                double buttonLeft = (button.TranslatePoint(default, window)
                    ?? throw new InvalidOperationException("Title button is not attached.")).X;
                balanceRight.Should().BeLessThanOrEqualTo(buttonLeft);
            }
        });
    }

    [Fact]
    public async Task MainWindow_WithoutManagementKey_HidesAccountBalance()
    {
        await DispatchAsync(async () =>
        {
            Mock<ISecretStore> store = new();
            store.Setup(service => service.GetSecretAsync(OpenRouterManagementKeySettingDefinition.SecretNameValue,
                It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
            using MainWindowTestContext context = new(services => services.AddSingleton(store.Object));
            MainWindow window = context.Window;
            MainWindowViewModel viewModel = window.DataContext.Should().BeOfType<MainWindowViewModel>().Subject;

            await viewModel.OpenRouterBalance.RefreshAsync(CancellationToken.None);
            window.Show();
            window.CaptureRenderedFrame();

            TextBlock text = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(control => control.Classes.Contains("account-balance"));
            text.IsVisible.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(WindowState.Normal, 670d)]
    [InlineData(WindowState.Normal, 1040d)]
    [InlineData(WindowState.Maximized, 670d)]
    [InlineData(WindowState.Maximized, 1040d)]
    public async Task MainWindow_WhenShown_UsesContinuousFullHeightTitleButtonHitAreas(
        WindowState windowState,
        double width)
    {
        await DispatchAsync(() =>
        {
            const double ButtonWidth = 44d;
            const double TitleBarHeight = 58d;
            const double EdgeInset = 1d;
            const int TitleButtonCount = 7;
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;
            window.WindowState = windowState;
            window.Width = width;

            window.Show();
            window.CaptureRenderedFrame();

            Control titleBar = window.GetVisualDescendants().OfType<Control>()
                .Single(control => control.Name == TitleBarName);
            Point titleBarOrigin = titleBar.TranslatePoint(default, window)
                ?? throw new InvalidOperationException("The title bar must be attached to the window.");
            Button[] buttons = titleBar.GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible)
                .OrderBy(button => button.TranslatePoint(default, window)?.X)
                .ToArray();
            buttons.Should().HaveCount(TitleButtonCount);
            double previousRight = 0d;

            foreach (Button button in buttons)
            {
                Point origin = button.TranslatePoint(default, window)
                    ?? throw new InvalidOperationException("The title button must be attached to the window.");
                button.Bounds.Size.Should().Be(new Size(ButtonWidth, TitleBarHeight));
                origin.Y.Should().BeApproximately(titleBarOrigin.Y, PositionTolerance);

                if (previousRight > 0d)
                {
                    origin.X.Should().BeApproximately(previousRight, PositionTolerance);
                }

                previousRight = origin.X + button.Bounds.Width;
                Point[] hitPoints =
                [
                    new Point(ButtonWidth / 2d, EdgeInset),
                    new Point(ButtonWidth / 2d, TitleBarHeight - EdgeInset),
                    new Point(EdgeInset, TitleBarHeight / 2d),
                    new Point(ButtonWidth - EdgeInset, TitleBarHeight / 2d)
                ];

                foreach (Point hitPoint in hitPoints)
                {
                    Point position = button.TranslatePoint(hitPoint, window)
                        ?? throw new InvalidOperationException("The title button must be attached to the window.");
                    Visual hit = window.InputHitTest(position).Should().BeAssignableTo<Visual>().Subject;
                    hit.GetVisualAncestors().Prepend(hit).Should().Contain(button);
                }
            }

            previousRight.Should().BeApproximately(window.ClientSize.Width, PositionTolerance);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TitleActions_WhenHovered_UseTheWindowControlFeedback()
    {
        await DispatchAsync(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;
            window.Resources["SukiPrimaryColor25"] = Brushes.Blue;
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            window.CaptureRenderedFrame();
            Button pinButton = window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "PART_PinButton");
            Point pinCenter = pinButton.TranslatePoint(
                new Point(pinButton.Bounds.Width / 2d, pinButton.Bounds.Height / 2d), window)
                ?? throw new InvalidOperationException("The pin button must be attached to the window.");
            BrushTransition pinTransition = pinButton.Transitions.Should().ContainSingle().Which
                .Should().BeOfType<BrushTransition>().Subject;
            IBrush? defaultBackground = pinButton.Background;
            pinButton.Transitions = null;

            window.MouseMove(pinCenter);

            pinButton.IsPointerOver.Should().BeTrue();
            IBrush? hoverBackground = pinButton.Background;
            hoverBackground.Should().NotBeNull().And.NotBe(defaultBackground);

            foreach (Button button in window.RightWindowTitleBarControls.OfType<Button>())
            {
                Point center = button.TranslatePoint(
                    new Point(button.Bounds.Width / 2d, button.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The title action must be attached to the window.");
                BrushTransition transition = button.Transitions.Should().ContainSingle().Which
                    .Should().BeOfType<BrushTransition>().Subject;
                transition.Property.Should().Be(pinTransition.Property);
                transition.Duration.Should().Be(pinTransition.Duration);
                button.Transitions = null;

                window.MouseMove(center);

                button.IsPointerOver.Should().BeTrue();
                button.Background.Should().Be(hoverBackground);
                button.BorderThickness.Should().Be(pinButton.BorderThickness);
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task MainWindow_WhenShown_RendersDlss5TitleIconInNvidiaGreen()
    {
        await DispatchAsync(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;

            window.Show();
            window.CaptureRenderedFrame();

            MainWindowViewModel viewModel = window.DataContext.Should()
                .BeOfType<MainWindowViewModel>().Subject;
            Button button = window.GetVisualDescendants().OfType<Button>()
                .Single(candidate => ReferenceEquals(candidate.Command, viewModel.OpenDlss5Command));
            PathIcon icon = button.Content.Should().BeOfType<PathIcon>().Subject;
            ISolidColorBrush brush = icon.Foreground.Should().BeAssignableTo<ISolidColorBrush>().Subject;

            brush.Color.Should().Be(Color.Parse("#76B900"));
            Button settingsButton = window.GetVisualDescendants().OfType<Button>()
                .Single(candidate => ReferenceEquals(candidate.Command, viewModel.OpenSettingsCommand));
            Point dlssPosition = button.TranslatePoint(default, window)
                ?? throw new InvalidOperationException("DLSS button position was not found.");
            Point settingsPosition = settingsButton.TranslatePoint(default, window)
                ?? throw new InvalidOperationException("Settings button position was not found.");
            dlssPosition.X.Should().BeLessThan(settingsPosition.X);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task MainWindow_WhenUpdateIsAvailable_ShowsSukiToastActions()
    {
        await DispatchAsync(async () =>
        {
            Mock<IApplicationUpdateService> updateServiceMock =
                MainWindowLayoutTests.CreateAvailableUpdateServiceMock();

            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton(updateServiceMock.Object);
            });
            MainWindow window = context.Window;

            window.Show();
            await MainWindowLayoutTests.ShowAvailableUpdateAsync(window);

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
    public async Task MainWindow_WhenShown_ConfiguresGenerationPanelResizeGrip()
    {
        await DispatchAsync(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;

            window.Show();
            window.CaptureRenderedFrame();

            GridSplitter resizeGrip = window
                .GetVisualDescendants()
                .OfType<GridSplitter>()
                .Single(splitter => string.Equals(splitter.Name, GenerationPanelResizeGripName, StringComparison.Ordinal));
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

            Border panel = window.FindControl<Border>(GenerationPanelHostName)
                ?? throw new InvalidOperationException("The generation panel was not found.");
            CenteredWidthResizeGrip[] widthGrips = window.GetVisualDescendants()
                .OfType<CenteredWidthResizeGrip>().ToArray();
            widthGrips.Should().HaveCount(2);
            widthGrips.Select(grip => grip.HorizontalAlignment).Should()
                .BeEquivalentTo(new HorizontalAlignment[] { HorizontalAlignment.Left, HorizontalAlignment.Right });

            foreach (CenteredWidthResizeGrip grip in widthGrips)
            {
                grip.Target.Should().BeSameAs(panel);
                grip.ResizeContainer.Should().BeSameAs(shellContentGrid);
                grip.MinimumTargetWidth.Should().Be(GetDoubleResource(window, GenerationPanelWidthResourceKey));
                grip.Width.Should().Be(resizeGrip.Height);
                grip.VerticalAlignment.Should().Be(VerticalAlignment.Stretch);
                grip.ResizeDirection.Should().Be(GridResizeDirection.Columns);
                grip.ShowsPreview.Should().BeFalse();
                grip.Cursor?.ToString().Should().Be(nameof(StandardCursorType.SizeWestEast));
            }
        });
    }

    [Fact]
    public async Task Dlss5BackCommand_WhenPanelIsOpen_HidesPanelWithoutStoppingEngine()
    {
        await DispatchAsync(() =>
        {
            Mock<IDlss5NativeEngine> nativeEngine = new();
            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton(nativeEngine.Object);
            });
            MainWindowViewModel viewModel = context.Window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;
            viewModel.Dlss5.IsOpen = true;

            viewModel.Dlss5.BackCommand.Execute(null);

            viewModel.Dlss5.IsOpen.Should().BeFalse();
            nativeEngine.Verify(engine => engine.Stop(), Times.Never);
        });
    }

    [Fact]
    public async Task Dlss5CloseCommand_WhenPanelIsOpen_StopsEngineAndRenderingAnimation()
    {
        await DispatchAsync(() =>
        {
            Mock<IDlss5NativeEngine> nativeEngine = new();
            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton(nativeEngine.Object);
            });
            MainWindow window = context.Window;
            MainWindowViewModel viewModel = context.Window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;
            viewModel.Dlss5.IsOpen = true;
            viewModel.Dlss5.IsRendering = true;
            window.Show();
            window.CaptureRenderedFrame();
            ProgressBar renderingProgress = window
                .GetVisualDescendants()
                .OfType<Dlss5SessionView>()
                .Single()
                .GetVisualDescendants()
                .OfType<ProgressBar>()
                .Single();

            renderingProgress.IsIndeterminate.Should().BeTrue();

            viewModel.Dlss5.CloseCommand.Execute(null);

            viewModel.Dlss5.IsOpen.Should().BeFalse();
            renderingProgress.IsIndeterminate.Should().BeFalse();
            nativeEngine.Verify(engine => engine.Stop(), Times.Once);
        });
    }

    [Fact]
    public async Task MainWindow_WhenGenerationPanelPaddingIsRightClicked_OpensResetMenu()
    {
        await DispatchAsync(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;
            window.Show();
            window.CaptureRenderedFrame();

            Border panelContent = window
                .FindControl<Border>("GenerationPanelContent")
                ?? throw new InvalidOperationException("Generation panel content was not found.");
            AnimatedContextMenuFlyout menuFlyout = panelContent.Resources["GenerationPanelContextFlyout"]
                .Should()
                .BeOfType<AnimatedContextMenuFlyout>()
                .Subject;
            Point paddingPoint = panelContent.TranslatePoint(
                    new Point(2d, 2d),
                    window)
                ?? throw new InvalidOperationException("Generation panel padding position was not found.");

            window.MouseDown(paddingPoint, MouseButton.Right);

            menuFlyout.IsOpen.Should().BeTrue();
            MenuItem resetMenuItem = window
                .FindControl<MenuItem>("ResetGenerationPanelMenuItem")
                ?? throw new InvalidOperationException("Reset menu item was not found.");
            PathIcon resetIcon = resetMenuItem.Icon
                .Should()
                .BeOfType<PathIcon>()
                .Subject;
            Avalonia.Controls.Shapes.Path resetIconShape = resetIcon
                .GetVisualDescendants()
                .OfType<Avalonia.Controls.Shapes.Path>()
                .Single();
            resetIconShape.Fill.Should().NotBeNull();
            resetIconShape.Stroke.Should().BeNull();
            MainWindowViewModel viewModel = window.DataContext
                .Should()
                .BeOfType<MainWindowViewModel>()
                .Subject;
            resetMenuItem.Command.Should().BeSameAs(
                viewModel.ActiveGenerationPanel.ResetPanelCommand);
            menuFlyout.Hide();
        });
    }

    [Fact]
    public async Task MainWindow_WhenGenerationControlIsRightClicked_OpensResetMenuOnce()
    {
        await DispatchAsync(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;
            window.Show();
            window.CaptureRenderedFrame();

            Border panelContent = window
                .FindControl<Border>("GenerationPanelContent")
                ?? throw new InvalidOperationException("Generation panel content was not found.");
            AnimatedContextMenuFlyout menuFlyout = panelContent.Resources["GenerationPanelContextFlyout"]
                .Should()
                .BeOfType<AnimatedContextMenuFlyout>()
                .Subject;
            ComboBox resolutionComboBox = window
                .GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(
                    comboBox.Name,
                    "ResolutionComboBox",
                    StringComparison.Ordinal));
            Point comboBoxPoint = resolutionComboBox.TranslatePoint(
                    new Point(resolutionComboBox.Bounds.Width / 2d, resolutionComboBox.Bounds.Height / 2d),
                    window)
                ?? throw new InvalidOperationException("Resolution combo box position was not found.");
            int openedCount = 0;
            menuFlyout.Opened += OnMenuFlyoutOpened;

            try
            {
                window.MouseDown(comboBoxPoint, MouseButton.Right);

                openedCount.Should().Be(1);
                menuFlyout.IsOpen.Should().BeTrue();
            }
            finally
            {
                menuFlyout.Opened -= OnMenuFlyoutOpened;
                menuFlyout.Hide();
            }

            void OnMenuFlyoutOpened(object? sender, EventArgs eventArgs)
            {
                _ = sender;
                _ = eventArgs;
                openedCount++;
            }
        });
    }

    [Fact]
    public async Task MainWindow_WhenUpdateToastIsShown_FollowsUiScaleAndKeepsBottomRightAnchor()
    {
        await DispatchAsync(async () =>
        {
            Mock<IApplicationUpdateService> updateServiceMock =
                MainWindowLayoutTests.CreateAvailableUpdateServiceMock();
            RecordingUiScaleService uiScaleService = new(InitialUiScale);
            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton(updateServiceMock.Object);
                services.AddSingleton<IUiScaleService>(uiScaleService);
            });
            MainWindow window = context.Window;
            window.Show();
            await MainWindowLayoutTests.ShowAvailableUpdateAsync(window);

            SukiToastHost toastHost = window
                .GetVisualDescendants()
                .OfType<SukiToastHost>()
                .Single(host => string.Equals(host.Name, UpdateToastHostName, StringComparison.Ordinal));
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
            toastHost.RenderTransformOrigin.Should().Be(MainWindowLayoutTests.BottomRightOrigin);
            anchorAfterScaleChange.X.Should().BeApproximately(
                anchorBeforeScaleChange.X,
                PositionTolerance);
            anchorAfterScaleChange.Y.Should().BeApproximately(
                anchorBeforeScaleChange.Y,
                PositionTolerance);
        });
    }

    [Fact]
    public async Task MainWindow_WhenShown_KeepsConfirmationHostBelowTitleBar()
    {
        await DispatchAsync(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;

            window.Show();
            window.CaptureRenderedFrame();

            SukiDialogHost dialogHost = window
                .GetVisualDescendants()
                .OfType<SukiDialogHost>()
                .Single(host => string.Equals(host.Name, ConfirmationDialogHostName, StringComparison.Ordinal));
            Control titleBar = window
                .GetVisualDescendants()
                .OfType<Control>()
                .Single(control => string.Equals(control.Name, TitleBarName, StringComparison.Ordinal));
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
    public async Task MainWindow_WhenShown_ConfiguresFasterConfirmationDialogTransitions()
    {
        await DispatchAsync(() =>
        {
            using MainWindowTestContext context = new();
            MainWindow window = context.Window;

            window.Show();
            window.CaptureRenderedFrame();

            SukiDialogHost dialogHost = window
                .GetVisualDescendants()
                .OfType<SukiDialogHost>()
                .Single(host => string.Equals(host.Name, ConfirmationDialogHostName, StringComparison.Ordinal));
            ContentControl dialogContent = dialogHost
                .GetVisualDescendants()
                .OfType<ContentControl>()
                .Single(control => string.Equals(
                    control.Name,
                    "PART_DialogContent",
                    StringComparison.Ordinal));
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
                MainWindowLayoutTests.ConfirmationDialogTransitionDuration);
            opacityTransition.Duration.Should().Be(
                MainWindowLayoutTests.ConfirmationDialogOpacityTransitionDuration);
            transformTransition.Duration.Should().Be(
                MainWindowLayoutTests.ConfirmationDialogTransitionDuration);
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
                MainWindowLayoutTests.CreateDeletionConfirmationRequest(),
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
            Color popupBackground = MainWindowLayoutTests.GetColorResource(
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
            bool result = await resultTask.WaitAsync(MainWindowLayoutTests.DialogCompletionTimeout);

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
                MainWindowLayoutTests.CreateDeletionConfirmationRequest(),
                CancellationToken.None);
            window.CaptureRenderedFrame();
            KeyEventArgs keyEventArgs = new()
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
                PhysicalKey = PhysicalKey.Escape
            };

            window.RaiseEvent(keyEventArgs);
            bool result = await resultTask.WaitAsync(MainWindowLayoutTests.DialogCompletionTimeout);

            result.Should().BeFalse();
            viewModel.Gallery.IsSelectionMode.Should().BeTrue();
            item.IsSelected.Should().BeTrue();
        });
    }

    [Fact]
    public async Task MainWindow_WhenShown_ProvidesPanelBoundaryForAttachmentDrag()
    {
        await DispatchAsync(() =>
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

    [Theory]
    [InlineData(HorizontalAlignment.Left)]
    [InlineData(HorizontalAlignment.Right)]
    public async Task GenerationPanel_WhenResizedAndReopened_RestoresBothDimensions(HorizontalAlignment alignment)
    {
        await DispatchAsync(async () =>
        {
            RecordingStateWriteScheduler scheduler = new();
            GenerationPanelLayoutState saved;
            using (MainWindowTestContext context = new(layoutScheduler: scheduler))
            {
                MainWindow window = context.Window;
                window.Width = 1600d;
                window.Height = 1000d;
                window.Show();
                window.CaptureRenderedFrame();
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                CenteredWidthResizeGrip widthGrip = GetWidthResizeGrip(window, alignment);
                GridSplitter heightGrip = window.FindControl<GridSplitter>(GenerationPanelResizeGripName)
                    ?? throw new InvalidOperationException("The generation panel height grip was not found.");
                Grid container = window.FindControl<Grid>(ShellContentGridName)
                    ?? throw new InvalidOperationException("The generation panel container was not found.");
                Border panel = window.FindControl<Border>(GenerationPanelHostName)
                    ?? throw new InvalidOperationException("The generation panel was not found.");
                Point widthStart = widthGrip.TranslatePoint(
                    new Point(widthGrip.Bounds.Width / 2d, widthGrip.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The width grip was not attached.");
                Vector widthDelta = new(alignment == HorizontalAlignment.Left ? -100d : 100d, 0d);

                window.MouseDown(widthStart, MouseButton.Left);
                window.MouseMove(widthStart + widthDelta);
                window.CaptureRenderedFrame();
                window.MouseUp(widthStart + widthDelta, MouseButton.Left);
                Point heightStart = heightGrip.TranslatePoint(
                    new Point(heightGrip.Bounds.Width / 2d, heightGrip.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The height grip was not attached.");
                Vector heightDelta = new(0d, -120d);
                window.MouseDown(heightStart, MouseButton.Left);
                window.MouseMove(heightStart + heightDelta);
                window.CaptureRenderedFrame();
                window.MouseUp(heightStart + heightDelta, MouseButton.Left);

                saved = scheduler.SavedState.Should().BeOfType<GenerationPanelLayoutState>().Subject;
                saved.Width.Should().Be(panel.MaxWidth).And.BeGreaterThan(
                    GetDoubleResource(panel, GenerationPanelWidthResourceKey));
                saved.Height.Should().Be(container.RowDefinitions[GenerationPanelRowIndex].Height.Value)
                    .And.BeGreaterThan(container.RowDefinitions[GenerationPanelRowIndex].MinHeight);
            }

            using MainWindowTestContext restoredContext = new(layoutState: saved);
            MainWindow restoredWindow = restoredContext.Window;
            restoredWindow.Width = 1600d;
            restoredWindow.Height = 1000d;
            restoredWindow.Show();
            restoredWindow.CaptureRenderedFrame();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            restoredWindow.CaptureRenderedFrame();

            Border restoredPanel = restoredWindow.FindControl<Border>(GenerationPanelHostName)
                ?? throw new InvalidOperationException("The generation panel was not found.");
            Grid restoredContainer = restoredWindow.FindControl<Grid>(ShellContentGridName)
                ?? throw new InvalidOperationException("The generation panel container was not found.");
            restoredPanel.MaxWidth.Should().Be(saved.Width);
            restoredPanel.Bounds.Width.Should().BeApproximately(saved.Width.GetValueOrDefault(), PositionTolerance);
            restoredContainer.RowDefinitions[GenerationPanelRowIndex].ActualHeight.Should()
                .BeApproximately(saved.Height.GetValueOrDefault(), HeightTolerance);
        });
    }

    [Fact]
    public async Task GenerationPanel_WithSavedSizeLargerThanWindow_ClampsLayoutAndPreservesPreference()
    {
        await DispatchAsync(async () =>
        {
            GenerationPanelLayoutState saved = new() { Width = 1200d, Height = 900d };
            RecordingStateWriteScheduler scheduler = new();
            using MainWindowTestContext context = new(layoutState: saved, layoutScheduler: scheduler);
            MainWindow window = context.Window;
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            window.Show();
            window.CaptureRenderedFrame();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            window.CaptureRenderedFrame();
            Border panel = window.FindControl<Border>(GenerationPanelHostName)
                ?? throw new InvalidOperationException("The generation panel was not found.");
            Grid container = window.FindControl<Grid>(ShellContentGridName)
                ?? throw new InvalidOperationException("The generation panel container was not found.");
            RowDefinition row = container.RowDefinitions[GenerationPanelRowIndex];

            panel.MaxWidth.Should().Be(saved.Width);
            row.Height.Value.Should().Be(saved.Height);
            panel.Bounds.Width.Should().BeLessThan(saved.Width.GetValueOrDefault());
            row.ActualHeight.Should().BeLessThanOrEqualTo(container.Bounds.Height);
            scheduler.CallCount.Should().Be(0);

            window.Width = 1600d;
            window.Height = 1400d;
            window.CaptureRenderedFrame();

            panel.Bounds.Width.Should().BeApproximately(saved.Width.GetValueOrDefault(), PositionTolerance);
            row.ActualHeight.Should().BeApproximately(saved.Height.GetValueOrDefault(), HeightTolerance);
            scheduler.CallCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task GenerationPanel_WhenScaledWindowNarrows_ShrinksFromPreferredWidthAndRemainsContained()
    {
        await DispatchAsync(() =>
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
                .Single(border => string.Equals(border.Name, GenerationPanelHostName, StringComparison.Ordinal));
            Grid shellContentGrid = window
                .GetVisualDescendants()
                .OfType<Grid>()
                .Single(grid => string.Equals(grid.Name, ShellContentGridName, StringComparison.Ordinal));
            double preferredWidth = MainWindowLayoutTests.GetDoubleResource(
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

    [Theory]
    [InlineData(HorizontalAlignment.Left, 0.6d)]
    [InlineData(HorizontalAlignment.Left, 1d)]
    [InlineData(HorizontalAlignment.Left, 1.5d)]
    [InlineData(HorizontalAlignment.Right, 0.6d)]
    [InlineData(HorizontalAlignment.Right, 1d)]
    [InlineData(HorizontalAlignment.Right, 1.5d)]
    public async Task GenerationPanel_WhenWidthGripIsDragged_FollowsPointerAndRemainsCentered(
        HorizontalAlignment alignment,
        double uiScale)
    {
        await DispatchAsync(() =>
        {
            RecordingUiScaleService uiScaleService = new(uiScale);
            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton<IUiScaleService>(uiScaleService);
            });
            MainWindow window = context.Window;
            window.Width = 1600d;
            window.Show();
            window.CaptureRenderedFrame();
            Border panel = window.FindControl<Border>(GenerationPanelHostName)
                ?? throw new InvalidOperationException("The generation panel was not found.");
            CenteredWidthResizeGrip grip = GetWidthResizeGrip(window, alignment);
            Grid container = window.FindControl<Grid>(ShellContentGridName)
                ?? throw new InvalidOperationException("The generation panel container was not found.");
            Point start = grip.TranslatePoint(new Point(grip.Bounds.Width / 2d, grip.Bounds.Height / 2d), window)
                ?? throw new InvalidOperationException("The generation panel width grip position was not found.");
            double originalWidth = panel.Bounds.Width;
            double direction = alignment == HorizontalAlignment.Left ? -1d : 1d;

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(direction * 100d, 0d));
            window.CaptureRenderedFrame();
            window.MouseMove(start + new Vector(direction * 150d, 0d));
            window.CaptureRenderedFrame();
            window.MouseUp(start + new Vector(direction * 150d, 0d), MouseButton.Left);

            panel.MaxWidth.Should().BeApproximately(originalWidth + 300d / uiScale, PositionTolerance);
            double pixelTolerance = 1d / (window.RenderScaling * uiScale);
            panel.Bounds.Center.X.Should().BeApproximately(container.Bounds.Width / 2d, pixelTolerance);
        });
    }

    [Theory]
    [InlineData(HorizontalAlignment.Left)]
    [InlineData(HorizontalAlignment.Right)]
    public async Task GenerationPanel_WhenWidthGripCrossesOptionsThreshold_AnimatesWithWindowFrameScheduler(
        HorizontalAlignment alignment)
    {
        await DispatchAsync(async () =>
        {
            ImageModelOptionCatalog modelCatalog = new();
            modelCatalog.Initialize(ApiModelMetadataTestCatalog.LoadCatalog());
            using MainWindowTestContext context = new(services =>
            {
                services.AddSingleton<IImageModelOptionCatalog>(modelCatalog);
                services.AddSingleton<IUiScaleService>(new RecordingUiScaleService(1d));
            });
            MainWindow window = context.Window;
            window.Width = 1800d;
            window.Show();
            window.CaptureRenderedFrame();
            NanoBanana2PanelView view = window.GetVisualDescendants().OfType<NanoBanana2PanelView>().Single();
            UniversalNanoBananaPanelViewModel viewModel = view.DataContext.Should()
                .BeOfType<UniversalNanoBananaPanelViewModel>().Subject;
            viewModel.SelectedModel = viewModel.AvailableModels.Single(model =>
                model.Id == "openrouter-gpt-image-2-5-sunburst");
            window.CaptureRenderedFrame();
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
            ContentControl inlineHost = view.FindControl<ContentControl>("InlineGenerationOptionsHost")
                ?? throw new InvalidOperationException("The inline options host was not found.");
            CenteredWidthResizeGrip grip = GetWidthResizeGrip(window, alignment);
            double edgeDirection = alignment == HorizontalAlignment.Left ? -1d : 1d;
            inlineHost.IsVisible.Should().BeFalse();

            for (int direction = 1; direction >= -1; direction -= 2)
            {
                Point start = grip.TranslatePoint(new Point(grip.Bounds.Width / 2d, grip.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The panel width grip was not attached.");
                Point end = start + new Vector(direction * edgeDirection * 300d, 0d);
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(end);
                window.CaptureRenderedFrame();
                Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
                window.MouseUp(end, MouseButton.Left);
                inlineHost.IsVisible.Should().BeTrue($"the options should be transitioning in direction {direction}");
                inlineHost.IsHitTestVisible.Should().BeFalse();
                bool sawIntermediateFrame = false;

                for (int frame = 0; frame < 60; frame++)
                {
                    window.CaptureRenderedFrame();
                    sawIntermediateFrame = sawIntermediateFrame
                        || (inlineHost.Opacity > 0d && inlineHost.Opacity < 1d);

                    if (direction > 0 ? inlineHost.IsHitTestVisible : !inlineHost.IsVisible)
                    {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(16d));
                }

                sawIntermediateFrame.Should().BeTrue();
                inlineHost.IsVisible.Should().Be(direction > 0);
                inlineHost.IsHitTestVisible.Should().Be(direction > 0);
                window.GetVisualDescendants().OfType<NanoBanana2PanelView>().Single().Should().BeSameAs(view);
            }
        });
    }

    [Fact]
    public async Task MainWindow_WhenErrorIsShown_DisplaysSharedModalOverlay()
    {
        await DispatchAsync(() =>
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

    private static CenteredWidthResizeGrip GetWidthResizeGrip(MainWindow window, HorizontalAlignment alignment)
    {
        string gripName = alignment == HorizontalAlignment.Left ? LeftWidthResizeGripName : RightWidthResizeGripName;

        return window.FindControl<CenteredWidthResizeGrip>(gripName)
            ?? throw new InvalidOperationException($"The generation panel width grip '{gripName}' was not found.");
    }

    private static double GetDoubleResource(Control control, string resourceKey)
    {
        if ((control.TryFindResource(resourceKey, out object? value))
            && (value is double doubleValue))
        {
            return doubleValue;
        }

        throw new InvalidOperationException($"Double resource '{resourceKey}' was not found.");
    }

    private static Color GetColorResource(Control control, string resourceKey)
    {
        if ((control.TryFindResource(resourceKey, out object? value))
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

    private static void RegisterViewTemplates(MainWindow window, IServiceProvider serviceProvider)
    {
        window.DataTemplates.Add(
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

        public MainWindowTestContext(
            Action<ServiceCollection>? configureServices = null,
            GenerationPanelLayoutState? layoutState = null,
            RecordingStateWriteScheduler? layoutScheduler = null)
        {
            ServiceCollection services = new();
            services.AddSingleton(TestApiConfiguration.Create());
            services.AddDesktopServices();
            Mock<IAppStateStore> stateStore = new();
            stateStore.Setup(store => store.LoadAsync<GenerationPanelLayoutState>(
                    It.IsAny<IStateSection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(layoutState ?? new GenerationPanelLayoutState());
            services.AddTransient<GenerationPanelLayoutTracker>(provider => new GenerationPanelLayoutTracker(
                stateStore.Object,
                layoutScheduler ?? new RecordingStateWriteScheduler(),
                provider.GetRequiredService<GenerationPanelLayoutStateSection>(),
                provider.GetRequiredService<ILogger<GenerationPanelLayoutTracker>>()));
            configureServices?.Invoke(services);

            _serviceProvider = services.BuildServiceProvider();
            DialogService = _serviceProvider.GetRequiredService<IDialogService>();
            TextProvider =
                _serviceProvider.GetRequiredService<ILocalizationTextProvider>();
            Window = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindowLayoutTests.RegisterViewTemplates(Window, _serviceProvider);
        }

        public void Dispose()
        {
            Window.Close();
            _serviceProvider.Dispose();
        }
    }
}
