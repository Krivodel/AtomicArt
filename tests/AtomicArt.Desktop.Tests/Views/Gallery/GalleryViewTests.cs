using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Shell;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GalleryViewTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void GalleryViewKeyBindings_WhenRendered_MapSelectAllCommand()
    {
        Dispatch(() =>
        {
            using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                IReadOnlyDictionary<string, KeyBinding> bindings = scenario.View.KeyBindings
                    .OfType<KeyBinding>()
                    .ToDictionary(
                        binding => binding.Gesture?.ToString() ?? string.Empty,
                        binding => binding);

                bindings["Ctrl+A"].Command.Should().BeSameAs(
                    scenario.ViewModel.SelectAllCommand);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MainWindowKeyBindings_WhenRendered_MapGlobalSelectionCommands()
    {
        Dispatch(() =>
        {
            using ServiceProvider serviceProvider = CreateServiceProvider();
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);
            scenario.Window.Show();

            try
            {
                IReadOnlyDictionary<string, KeyBinding> bindings = scenario.Window.KeyBindings
                    .OfType<KeyBinding>()
                    .ToDictionary(
                        binding => binding.Gesture?.ToString() ?? string.Empty,
                        binding => binding);

                bindings["Escape"].Command.Should().BeSameAs(
                    scenario.ViewModel.Gallery.ExitSelectionModeCommand);
                bindings["Delete"].Command.Should().BeSameAs(
                    scenario.ViewModel.Gallery.DeleteSelectedCommand);
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public async Task MainWindowEscape_AfterSelectAllButton_ClearsSelection()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);
            GalleryItemState[] items = Enumerable
                .Range(1, 10)
                .Select(index => GalleryItemStateTestFactory.CreateGenerated(
                    prompt: $"Item {index}"))
                .ToArray();
            await scenario.ViewModel.RestoreGalleryAsync(
                items,
                CancellationToken.None);
            scenario.Window.Show();
            scenario.Window.CaptureRenderedFrame();

            try
            {
                GalleryViewModel gallery = scenario.ViewModel.Gallery;
                gallery.ToggleSelectionCommand.Execute(gallery.Items[0]);
                scenario.Window.CaptureRenderedFrame();
                GallerySelectionOverlayView selectionOverlay = scenario.Window
                    .GetVisualDescendants()
                    .OfType<GallerySelectionOverlayView>()
                    .Single();
                Button selectAllButton = selectionOverlay
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => ReferenceEquals(
                        button.Command,
                        gallery.SelectAllCommand));
                selectAllButton.Focus();

                gallery.SelectAllCommand.Execute(null);
                scenario.Window.CaptureRenderedFrame();

                gallery.SelectedCount.Should().Be(gallery.Items.Count);
                gallery.SelectAllCommand.CanExecute(null).Should().BeFalse();

                scenario.Window.KeyPress(
                    Key.Escape,
                    RawInputModifiers.None,
                    PhysicalKey.Escape,
                    null);
                scenario.Window.CaptureRenderedFrame();

                gallery.IsSelectionMode.Should().BeFalse();
                gallery.SelectedCount.Should().Be(0);
                gallery.Items.Should().OnlyContain(item => !item.IsSelected);
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public void GalleryViewBinding_WithViewModelFromContainer_PassesFacadeAndRegistersCoordinator()
    {
        Dispatch(() =>
        {
            ServiceCollection services = new();
            services.AddDesktopServices();
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                AssertGalleryViewOperations(scenario.View);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GalleryViewRestoreStateAsync_WithSavedItem_RendersVisibleCard()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                await scenario.ViewModel.RestoreStateAsync(
                    new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated() },
                    CancellationToken.None);
                window.CaptureRenderedFrame();

                AssertSingleVisibleCard(GetGalleryControl(scenario.View));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GalleryViewRestoreStateAsync_BeforeAttach_RendersVisibleCardAfterAttach()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);

            await scenario.ViewModel.RestoreStateAsync(
                new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated() },
                CancellationToken.None);

            Window window = Show(scenario.View);

            try
            {
                AssertSingleVisibleCard(GetGalleryControl(scenario.View));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GalleryViewSelection_WithSelectedItem_ShowsAnimatedSelectionStateOnCard()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                await scenario.ViewModel.RestoreStateAsync(
                    new GalleryItemState[]
                    {
                        GalleryItemStateTestFactory.CreateGenerated()
                    },
                    CancellationToken.None);
                scenario.ViewModel.Items.Should().ContainSingle();
                window.CaptureRenderedFrame();

                AnimatedGalleryControl gallery = GetGalleryControl(scenario.View);
                GenerationCardControl card = GetGalleryPanel(gallery)
                    .Children
                    .OfType<GenerationCardControl>()
                    .Single();
                Button toggleSelectionButton = card.FindControl<Button>("ToggleSelectionButton")
                    ?? throw new InvalidOperationException("Selection button was not found.");
                Border selectionHighlight = card
                    .FindControl<Border>("SelectionHighlight")
                    ?? throw new InvalidOperationException("Selection highlight was not found.");
                Border cardRoot = card.FindControl<Border>("GenerationCardRoot")
                    ?? throw new InvalidOperationException("Generation card root was not found.");
                Border cardContainer = card.FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException("Generation card container was not found.");
                Avalonia.Controls.Shapes.Path selectionCheck = toggleSelectionButton
                    .GetVisualDescendants()
                    .OfType<Avalonia.Controls.Shapes.Path>()
                    .Single();
                Avalonia.Media.Geometry selectionGeometry = selectionCheck.Data
                    ?? throw new InvalidOperationException("Selection geometry was not found.");
                ISolidColorBrush toggleBackground = toggleSelectionButton.Background
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Subject;
                ISolidColorBrush highlightBackground = selectionHighlight.Background
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Subject;
                ISolidColorBrush highlightBorder = selectionHighlight.BorderBrush
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Subject;
                toggleSelectionButton.IsVisible.Should().BeTrue(
                    "the selection check must be available before selection mode starts");
                toggleSelectionButton.Classes.Should().Contain("card-selection-toggle");
                toggleSelectionButton.Classes.Should().NotContain("selected");
                toggleSelectionButton.Width.Should().Be(28d);
                toggleSelectionButton.BorderThickness.Left.Should().Be(0d);
                toggleBackground.Color.Should().Be(Colors.Transparent);
                toggleSelectionButton.Opacity.Should().Be(0d,
                    "an unselected card shows its check only while hovered");
                selectionGeometry.Bounds.Center.X.Should().Be(8d);
                selectionGeometry.Bounds.Center.Y.Should().Be(8d);
                selectionCheck.HorizontalAlignment.Should().Be(HorizontalAlignment.Center);
                selectionCheck.VerticalAlignment.Should().Be(VerticalAlignment.Center);
                selectionCheck.Stretch.Should().Be(Stretch.None);
                selectionCheck.Stroke.Should().NotBeNull();
                selectionCheck.Effect.Should().BeOfType<DropShadowEffect>();
                selectionHighlight.Opacity.Should().Be(0d);
                highlightBackground.Color.Should().Be(GalleryHighlightPalette.BackgroundColor);
                highlightBorder.Color.Should().Be(GalleryHighlightPalette.BorderColor);
                selectionHighlight.BorderThickness.Should().Be(
                    GalleryHighlightPalette.BorderThickness);
                selectionHighlight.CornerRadius.Should().Be(cardRoot.CornerRadius);
                selectionHighlight.Effect.Should().BeNull();
                selectionHighlight.IsHitTestVisible.Should().BeFalse();
                selectionHighlight.Margin.Should().Be(default(Thickness));
                cardRoot.ClipToBounds.Should().BeTrue();
                cardContainer.ClipToBounds.Should().BeFalse();
                Point highlightPosition = selectionHighlight.TranslatePoint(
                    new Point(0d, 0d),
                    cardRoot)
                    ?? throw new InvalidOperationException("Selection highlight position was not found.");
                highlightPosition.Should().Be(default(Point));
                selectionHighlight.Bounds.Size.Should().Be(cardRoot.Bounds.Size);

                Canvas overlayCanvas = GetOverlayCanvas(gallery);
                GalleryLayoutService galleryLayout = new();
                galleryLayout.TryGetOverlayRect(
                        cardRoot,
                        overlayCanvas,
                        out Rect expectedCardSurfaceRect)
                    .Should()
                    .BeTrue();
                galleryLayout.TryGetCardSurface(
                        card,
                        overlayCanvas,
                        out Rect cardSurfaceRect,
                        out CornerRadius cardSurfaceCornerRadius)
                    .Should()
                    .BeTrue();
                cardSurfaceRect.Should().Be(expectedCardSurfaceRect);
                cardSurfaceCornerRadius.Should().Be(cardRoot.CornerRadius);

                Transitions highlightTransitions = selectionHighlight.Transitions
                    ?? throw new InvalidOperationException("Selection highlight transitions were not found.");
                DoubleTransition highlightTransition = highlightTransitions
                    .OfType<DoubleTransition>()
                    .Single();
                highlightTransition.Property.Should().Be(Visual.OpacityProperty);
                highlightTransition.Duration.Should().Be(TimeSpan.FromMilliseconds(150));
                toggleSelectionButton.Transitions = null;
                selectionHighlight.Transitions = null;

                Point cardCenter = GetControlCenter(card, window);
                window.MouseMove(cardCenter, RawInputModifiers.None);
                window.CaptureRenderedFrame();

                toggleSelectionButton.Opacity.Should().Be(1d,
                    "hovering an unselected card reveals its check");

                toggleSelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.CaptureRenderedFrame();

                gallery.IsSelectionMode.Should().BeTrue(
                    "clicking the check starts selection mode");
                card.IsSelectionMode.Should().BeTrue(
                    "the rendered card follows gallery selection mode");
                card.IsSelectionDimmed.Should().BeFalse(
                    "selected previews remain unchanged");
                toggleSelectionButton.IsVisible.Should().BeTrue();
                toggleSelectionButton.Classes.Should().Contain("selected");
                toggleSelectionButton.Opacity.Should().Be(1d);
                toggleSelectionButton.Background.Should().NotBe(Brushes.Transparent);
                selectionHighlight.Classes.Should().Contain("selected");
                selectionHighlight.Opacity.Should().Be(1d);

                Button selectionTarget = card
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => Grid.GetRowSpan(button) == 2);

                selectionTarget.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.CaptureRenderedFrame();

                gallery.IsSelectionMode.Should().BeFalse(
                    "removing the last selection closes selection mode");
                selectionHighlight.Classes.Should().NotContain("selected");
                selectionHighlight.Opacity.Should().Be(0d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GalleryViewSelection_AfterCheckStartsMode_KeepsCardHighlightsIndependent()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                await scenario.ViewModel.RestoreStateAsync(
                    new GalleryItemState[]
                    {
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "First"),
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "Second")
                    },
                    CancellationToken.None);
                window.CaptureRenderedFrame();

                IReadOnlyList<GenerationCardControl> cards = GetGalleryPanel(
                        GetGalleryControl(scenario.View))
                    .Children
                    .OfType<GenerationCardControl>()
                    .ToList();
                cards.Should().HaveCount(2);
                GenerationCardControl firstCard = cards[0];
                GenerationCardControl secondCard = cards[1];
                Button firstCardCheck = firstCard.FindControl<Button>("ToggleSelectionButton")
                    ?? throw new InvalidOperationException("Selection button was not found.");
                Button secondCardCheck = secondCard.FindControl<Button>("ToggleSelectionButton")
                    ?? throw new InvalidOperationException("Selection button was not found.");
                Border firstDimmingOverlay = firstCard.FindControl<Border>("SelectionDimmingOverlay")
                    ?? throw new InvalidOperationException("Selection dimming overlay was not found.");
                Border secondDimmingOverlay = secondCard.FindControl<Border>("SelectionDimmingOverlay")
                    ?? throw new InvalidOperationException("Selection dimming overlay was not found.");
                Border firstSelectionHighlight = firstCard
                    .FindControl<Border>("SelectionHighlight")
                    ?? throw new InvalidOperationException("Selection highlight was not found.");
                Border secondSelectionHighlight = secondCard
                    .FindControl<Border>("SelectionHighlight")
                    ?? throw new InvalidOperationException("Selection highlight was not found.");
                firstDimmingOverlay.Transitions = null;
                secondDimmingOverlay.Transitions = null;
                firstSelectionHighlight.Transitions = null;
                secondSelectionHighlight.Transitions = null;
                firstCardCheck.Transitions = null;
                secondCardCheck.Transitions = null;

                firstCardCheck.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.CaptureRenderedFrame();

                firstCard.IsSelectionDimmed.Should().BeFalse();
                secondCard.IsSelectionDimmed.Should().BeTrue();
                firstDimmingOverlay.Opacity.Should().Be(0d);
                secondDimmingOverlay.Opacity.Should().Be(1d);
                firstCardCheck.Classes.Should().Contain("selected");
                secondCardCheck.Classes.Should().NotContain("selected");
                firstSelectionHighlight.Opacity.Should().Be(1d);
                secondSelectionHighlight.Opacity.Should().Be(0d);

                Button secondCardSelectionTarget = secondCard
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => Grid.GetRowSpan(button) == 2);
                secondCardSelectionTarget.IsVisible.Should().BeTrue();

                Point secondCardCenter = GetControlCenter(secondCard, window);
                window.MouseDown(secondCardCenter, MouseButton.Left);
                window.MouseUp(secondCardCenter, MouseButton.Left);
                window.CaptureRenderedFrame();

                scenario.ViewModel.SelectedCount.Should().Be(2);
                scenario.ViewModel.Items.Should().OnlyContain(item => item.IsSelected);
                firstCard.IsSelectionDimmed.Should().BeFalse();
                secondCard.IsSelectionDimmed.Should().BeFalse();
                secondDimmingOverlay.Opacity.Should().Be(0d);
                secondCardCheck.Classes.Should().Contain("selected");
                firstSelectionHighlight.Opacity.Should().Be(1d);
                secondSelectionHighlight.Opacity.Should().Be(1d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GallerySelectionBrush_FromInactiveCheck_SelectsFirstAndVisitedCards()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                await scenario.ViewModel.RestoreStateAsync(
                    new GalleryItemState[]
                    {
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "First"),
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "Second"),
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "Third")
                    },
                    CancellationToken.None);
                window.CaptureRenderedFrame();

                IReadOnlyList<GenerationCardControl> cards = GetGalleryPanel(
                        GetGalleryControl(scenario.View))
                    .Children
                    .OfType<GenerationCardControl>()
                    .ToList();
                GenerationCardControl firstCard = cards.Single(card =>
                    ReferenceEquals(
                        card.DataContext,
                        scenario.ViewModel.Items[0]));
                GenerationCardControl secondCard = cards.Single(card =>
                    ReferenceEquals(
                        card.DataContext,
                        scenario.ViewModel.Items[1]));
                Button firstCardCheck = firstCard
                    .FindControl<Button>("ToggleSelectionButton")
                    ?? throw new InvalidOperationException(
                        "Selection button was not found.");
                Point firstCardCheckCenter = GetControlCenter(
                    firstCardCheck,
                    window);
                Point secondCardCenter = GetControlCenter(secondCard, window);

                window.MouseDown(firstCardCheckCenter, MouseButton.Left);
                window.MouseMove(
                    secondCardCenter,
                    RawInputModifiers.LeftMouseButton);
                window.MouseUp(secondCardCenter, MouseButton.Left);

                scenario.ViewModel.SelectedCount.Should().Be(2);
                scenario.ViewModel.Items[0].IsSelected.Should().BeTrue();
                scenario.ViewModel.Items[1].IsSelected.Should().BeTrue();
                scenario.ViewModel.Items[2].IsSelected.Should().BeFalse();
                scenario.ViewModel.IsSelectionMode.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GallerySelectionBrush_FromUnselectedCard_SelectsVisitedCards()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                await scenario.ViewModel.RestoreStateAsync(
                    new GalleryItemState[]
                    {
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "First"),
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "Second"),
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "Third")
                    },
                    CancellationToken.None);
                scenario.ViewModel.ToggleSelectionCommand.Execute(
                    scenario.ViewModel.Items[0]);
                window.CaptureRenderedFrame();

                IReadOnlyList<GenerationCardControl> cards = GetGalleryPanel(
                        GetGalleryControl(scenario.View))
                    .Children
                    .OfType<GenerationCardControl>()
                    .ToList();
                GenerationCardControl secondCard = cards.Single(card =>
                    ReferenceEquals(
                        card.DataContext,
                        scenario.ViewModel.Items[1]));
                GenerationCardControl thirdCard = cards.Single(card =>
                    ReferenceEquals(
                        card.DataContext,
                        scenario.ViewModel.Items[2]));
                Point secondCardCenter = GetControlCenter(secondCard, window);
                Point thirdCardCenter = GetControlCenter(thirdCard, window);

                window.MouseDown(secondCardCenter, MouseButton.Left);
                window.MouseMove(
                    thirdCardCenter,
                    RawInputModifiers.LeftMouseButton);
                window.MouseUp(thirdCardCenter, MouseButton.Left);

                scenario.ViewModel.SelectedCount.Should().Be(3);
                scenario.ViewModel.Items.Should().OnlyContain(item => item.IsSelected);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GallerySelectionBrush_FromSelectedCard_DeselectsVisitedCards()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            GalleryViewScenario scenario = CreateGalleryViewScenario(serviceProvider);
            Window window = Show(scenario.View);

            try
            {
                await scenario.ViewModel.RestoreStateAsync(
                    new GalleryItemState[]
                    {
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "First"),
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "Second"),
                        GalleryItemStateTestFactory.CreateGenerated(prompt: "Third")
                    },
                    CancellationToken.None);
                scenario.ViewModel.SelectAllCommand.Execute(null);
                window.CaptureRenderedFrame();

                IReadOnlyList<GenerationCardControl> cards = GetGalleryPanel(
                        GetGalleryControl(scenario.View))
                    .Children
                    .OfType<GenerationCardControl>()
                    .ToList();
                GenerationCardControl firstCard = cards.Single(card =>
                    ReferenceEquals(
                        card.DataContext,
                        scenario.ViewModel.Items[0]));
                GenerationCardControl secondCard = cards.Single(card =>
                    ReferenceEquals(
                        card.DataContext,
                        scenario.ViewModel.Items[1]));
                Point firstCardCenter = GetControlCenter(firstCard, window);
                Point secondCardCenter = GetControlCenter(secondCard, window);

                window.MouseDown(firstCardCenter, MouseButton.Left);
                window.MouseMove(
                    secondCardCenter,
                    RawInputModifiers.LeftMouseButton);
                window.MouseUp(secondCardCenter, MouseButton.Left);

                scenario.ViewModel.SelectedCount.Should().Be(1);
                scenario.ViewModel.Items[0].IsSelected.Should().BeFalse();
                scenario.ViewModel.Items[1].IsSelected.Should().BeFalse();
                scenario.ViewModel.Items[2].IsSelected.Should().BeTrue();
                scenario.ViewModel.IsSelectionMode.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task MainWindowRestoreGalleryAsync_BeforeShow_RendersVisibleGalleryCardAfterShow()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);

            await scenario.ViewModel.RestoreGalleryAsync(
                new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated() },
                CancellationToken.None);

            ShowAndAssertSingleVisibleCard(scenario.Window);
        });
    }

    [Fact]
    public async Task MainWindowSelection_WithSelectedItem_CrossfadesGenerationPanelContentWithoutBlur()
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);
            await scenario.ViewModel.RestoreGalleryAsync(
                new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated() },
                CancellationToken.None);
            scenario.Window.Show();
            scenario.Window.CaptureRenderedFrame();

            try
            {
                GallerySelectionOverlayView selectionOverlay = scenario.Window
                    .GetVisualDescendants()
                    .OfType<GallerySelectionOverlayView>()
                    .Single();
                Border generationPanelContent = scenario.Window
                    .FindControl<Border>("GenerationPanelContent")
                    ?? throw new InvalidOperationException(
                        "Generation panel content was not found.");
                Transitions contentTransitions = generationPanelContent.Transitions
                    ?? throw new InvalidOperationException(
                        "Generation panel content transitions were not found.");
                DoubleTransition contentTransition = contentTransitions
                    .OfType<DoubleTransition>()
                    .Single();
                Grid shellContent = scenario.Window.FindControl<Grid>("ShellContentGrid")
                    ?? throw new InvalidOperationException("Shell content was not found.");
                GenerationItemViewModel item = scenario.ViewModel.Gallery.Items.Single();

                selectionOverlay.IsActive.Should().BeFalse();
                selectionOverlay.GetVisualDescendants()
                    .OfType<BlurBackdropControl>()
                    .Should()
                    .BeEmpty();
                generationPanelContent.Opacity.Should().Be(1d);
                generationPanelContent.IsHitTestVisible.Should().BeTrue();
                generationPanelContent.Classes.Should().NotContain("selection-mode");
                contentTransition.Property.Should().Be(Visual.OpacityProperty);
                contentTransition.Duration.Should().Be(TimeSpan.FromMilliseconds(180d));
                ImageDropBehavior.GetIsEnabled(shellContent).Should().BeTrue();
                generationPanelContent.Transitions = null;

                scenario.ViewModel.Gallery.ToggleSelectionCommand.Execute(item);
                scenario.Window.CaptureRenderedFrame();

                selectionOverlay.IsActive.Should().BeTrue();
                generationPanelContent.Opacity.Should().Be(0d);
                generationPanelContent.IsHitTestVisible.Should().BeFalse();
                generationPanelContent.Classes.Should().Contain("selection-mode");
                ImageDropBehavior.GetIsEnabled(shellContent).Should().BeFalse();
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public async Task MainWindowRestoreAppStateCommand_BeforeShow_RendersVisibleGalleryCardAfterShow()
    {
        await DispatchAsync(async () =>
        {
            GalleryItemState savedItem = GalleryItemStateTestFactory.CreateGenerated();
            await using ServiceProvider serviceProvider = CreateServiceProvider(
                new FixedGalleryAppStateBootstrapper(savedItem));
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);

            await scenario.ViewModel.RestoreAppStateCommand.ExecuteAsync(null);

            ShowAndAssertSingleVisibleCard(scenario.Window);
        });
    }

    [Fact]
    public async Task MainWindowRestoreAppStateCommand_FireAndForgetBeforeShow_RendersVisibleGalleryCardAfterShow()
    {
        await DispatchAsync(async () =>
        {
            GalleryItemState savedItem = GalleryItemStateTestFactory.CreateGenerated();
            await using ServiceProvider serviceProvider = CreateServiceProvider(
                new FixedGalleryAppStateBootstrapper(savedItem));
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);

            Task restoreTask = scenario.ViewModel.RestoreAppStateCommand.ExecuteAsync(null);
            scenario.Window.Show();
            scenario.Window.CaptureRenderedFrame();

            try
            {
                await restoreTask;
                scenario.Window.CaptureRenderedFrame();

                AssertSingleVisibleCard(GetGalleryControl(scenario.Window));
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(30)]
    [InlineData(200)]
    public async Task MainWindowRestoreGalleryAsync_WithManySavedItems_RendersVisibleCards(int itemCount)
    {
        await DispatchAsync(async () =>
        {
            await using ServiceProvider serviceProvider = CreateServiceProvider();
            MainWindowScenario scenario = CreateMainWindowScenario(serviceProvider);
            IReadOnlyList<GalleryItemState> items = CreateSavedGalleryItems(itemCount);

            await scenario.ViewModel.RestoreGalleryAsync(items, CancellationToken.None);

            scenario.Window.Show();
            scenario.Window.CaptureRenderedFrame();

            try
            {
                scenario.ViewModel.Gallery.Items.Should().HaveCount(itemCount);
                AnimatedGalleryControl control = scenario.Window
                    .GetVisualDescendants()
                    .OfType<AnimatedGalleryControl>()
                    .Single();
                Canvas galleryPanel = GetGalleryPanel(control);

                galleryPanel.Children.OfType<Control>().Should().NotBeEmpty();
                galleryPanel.Children.OfType<Control>().Should().OnlyContain(card =>
                    card.IsVisible
                    && card.Opacity > 0d
                    && card.Width > 0d
                    && card.Height > 0d);
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    private static GalleryViewScenario CreateGalleryViewScenario(
        IServiceProvider serviceProvider)
    {
        GalleryViewModel viewModel = serviceProvider.GetRequiredService<GalleryViewModel>();
        GalleryView view = serviceProvider.GetRequiredService<GalleryView>();
        view.DataContext = viewModel;

        return new GalleryViewScenario(view, viewModel);
    }

    private static MainWindowScenario CreateMainWindowScenario(
        IServiceProvider serviceProvider)
    {
        RegisterGalleryViewTemplate(serviceProvider);
        MainWindow window = serviceProvider.GetRequiredService<MainWindow>();
        MainWindowViewModel viewModel = window.DataContext
            .Should()
            .BeOfType<MainWindowViewModel>()
            .Subject;

        return new MainWindowScenario(window, viewModel);
    }

    private static ServiceProvider CreateServiceProvider(
        IAppStateBootstrapper? appStateBootstrapper = null)
    {
        ServiceCollection services = new();
        services.AddSingleton(TestApiConfiguration.Create());
        services.AddDesktopServices();

        if (appStateBootstrapper is not null)
        {
            services.AddSingleton(appStateBootstrapper);
        }

        return services.BuildServiceProvider();
    }

    private static IReadOnlyList<GalleryItemState> CreateSavedGalleryItems(int count)
    {
        List<GalleryItemState> items = [];

        for (int i = 0; i < count; i++)
        {
            string prompt = string.Concat("Saved prompt ", i.ToString(CultureInfo.InvariantCulture));
            items.Add(GalleryItemStateTestFactory.CreateGenerated(prompt, i));
        }

        return items;
    }

    private static void AssertGalleryViewOperations(GalleryView view)
    {
        AnimatedGalleryControl control = GetGalleryControl(view);

        AnimatedGalleryOperations operations = control
            .Operations
            .Should()
            .BeOfType<AnimatedGalleryOperations>()
            .Subject;
        operations.ActiveOperations.Should().BeOfType<GalleryOperationCoordinator>();
    }

    private static void AssertSingleVisibleCard(AnimatedGalleryControl control)
    {
        Canvas galleryPanel = GetGalleryPanel(control);
        Control card = galleryPanel.Children.OfType<Control>().Single();

        card.IsVisible.Should().BeTrue();
        card.Opacity.Should().Be(1d);
        card.Width.Should().BeGreaterThan(0d);
        card.Height.Should().BeGreaterThan(0d);
    }

    private static void ShowAndAssertSingleVisibleCard(Window window)
    {
        window.Show();
        window.CaptureRenderedFrame();

        try
        {
            AssertSingleVisibleCard(GetGalleryControl(window));
        }
        finally
        {
            window.Close();
        }
    }

    private static Point GetControlCenter(
        Control control,
        Window window)
    {
        Point? controlCenter = control.TranslatePoint(
            new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
            window);

        return controlCenter
            ?? throw new InvalidOperationException(
                "Control position was not found.");
    }

    private static AnimatedGalleryControl GetGalleryControl(Avalonia.Visual visual)
    {
        return visual
            .GetVisualDescendants()
            .OfType<AnimatedGalleryControl>()
            .Single();
    }

    private static void RegisterGalleryViewTemplate(IServiceProvider serviceProvider)
    {
        Avalonia.Application.Current?.DataTemplates.Add(
            new ViewModelViewTemplate(
                new ViewTemplateRegistration[]
                {
                    new ViewTemplateRegistration(
                        typeof(GalleryViewModel),
                        serviceProvider.GetRequiredService<GalleryView>)
                }));
    }

    private sealed record MainWindowScenario(
        MainWindow Window,
        MainWindowViewModel ViewModel);

    private sealed record GalleryViewScenario(
        GalleryView View,
        GalleryViewModel ViewModel);

    private sealed class FixedGalleryAppStateBootstrapper : IAppStateBootstrapper
    {
        private readonly GalleryItemState _savedItem;

        public FixedGalleryAppStateBootstrapper(GalleryItemState savedItem)
        {
            _savedItem = savedItem;
        }

        public Task RestoreAsync(IAppStateRestoreTarget target, CancellationToken ct)
        {
            GalleryItemState[] savedItems = [_savedItem];

            return target.RestoreGalleryAsync(savedItems, ct);
        }

        public Task FlushAsync(IAppStateFlushTarget target, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
