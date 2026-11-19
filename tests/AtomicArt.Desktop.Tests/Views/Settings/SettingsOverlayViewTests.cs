using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services.Settings;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class SettingsOverlayViewTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public async Task Background_WhenShown_UsesPropertiesGradientsWithoutBlur()
    {
        await DispatchAsync(() =>
        {
            SettingsOverlayView view = new();
            Window window = Show(view);

            try
            {
                window.CaptureRenderedFrame();
                ModalOverlayControl panel = view
                    .GetVisualDescendants()
                    .OfType<ModalOverlayControl>()
                    .Single();
                Border header = panel
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Single(control => control.Name == "PART_Header");

                panel.Background.Should().BeSameAs(panel.FindResource("MetadataSurfaceBrush"));
                header.Background.Should().BeSameAs(panel.FindResource("MetadataHeaderBrush"));
                panel.BlurRadius.Should().Be(0d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(1024d)]
    [InlineData(670d)]
    public async Task Interface_WhenShown_AlignsSwitchesAtRightEdgeOfRows(double width)
    {
        await DispatchAsync(() =>
        {
            ImageViewerWindowLimitSettingsTestContext context = new(new SettingsState());
            using ImageViewerWindowLimitSettingViewModel limit = context.CreateEditor();
            limit.Count = limit.Maximum;
            using BooleanSettingViewModel confirmDeletion = new(
                new ConfirmDeletionSettingDefinition(), Mock.Of<IBooleanSettingValueSource>(),
                Mock.Of<ISettingsStateService>(), new BooleanSettingValueConverter(),
                new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);
            using SettingsViewModel viewModel = CreateSettingsViewModel([limit, confirmDeletion]);
            SettingsOverlayView view = new() { DataContext = viewModel };
            view.DataTemplates.Add(new FuncDataTemplate<ImageViewerWindowLimitSettingViewModel>(
                (_, _) => new ImageViewerWindowLimitSettingView()));
            view.DataTemplates.Add(new FuncDataTemplate<BooleanSettingViewModel>((_, _) => new BooleanSettingView()));

            Show(view, width, 768d, _ =>
            {
                Control limitView = view.GetVisualDescendants().OfType<ImageViewerWindowLimitSettingView>().Single();
                Control deletionView = view.GetVisualDescendants().OfType<BooleanSettingView>().Single();
                TextBlock limitLabel = GetVisibleSettingLabel(limitView);
                TextBlock deletionLabel = GetVisibleSettingLabel(deletionView);
                Border limitTrack = GetSwitchTrack(limitView);
                Border deletionTrack = GetSwitchTrack(deletionView);
                NumericUpDown input = limitView.GetVisualDescendants().OfType<NumericUpDown>().Single();
                object? captionBrush = view.FindResource("MetadataSecondaryTextBrush");

                limitLabel.Foreground.Should().BeSameAs(captionBrush);
                deletionLabel.Foreground.Should().BeSameAs(captionBrush);
                deletionLabel.FontWeight.Should().Be(limitLabel.FontWeight);
                (limitTrack.TranslatePoint(default, view)?.X)
                    .Should().Be(deletionTrack.TranslatePoint(default, view)?.X);
                (limitTrack.TranslatePoint(new Point(limitTrack.Bounds.Width, limitTrack.Bounds.Height / 2d), limitView)?.X)
                    .Should().Be(limitView.Bounds.Width);
                (limitTrack.TranslatePoint(new Point(0d, limitTrack.Bounds.Height / 2d), limitLabel)?.Y)
                    .Should().BeApproximately(limitLabel.Bounds.Height / 2d, 1d);
                (deletionTrack.TranslatePoint(new Point(0d, deletionTrack.Bounds.Height / 2d), deletionLabel)?.Y)
                    .Should().BeApproximately(deletionLabel.Bounds.Height / 2d, 1d);
                input.Value.Should().Be(limit.Maximum);
                input.Bounds.Width.Should().BeLessThan(limitView.Bounds.Width / 3d);
                (input.TranslatePoint(new Point(input.Bounds.Width, 0d), limitTrack)?.X)
                    .Should().BeInRange(-20d, 0d);
            });
        });
    }

    [Theory]
    [InlineData(1024d, 768d, 660d)]
    [InlineData(670d, 340d, 630d)]
    public async Task Sections_WhenClicked_FadeContentWithoutMovingTabs(double width, double height, double panelWidth)
    {
        await DispatchAsync(async () =>
        {
            ISettingItemViewModel[] settings =
            [
                CreateSetting(SettingsSections.Connection),
                CreateSetting(SettingsSections.Models),
                CreateSetting(SettingsSections.Appearance),
                CreateSetting(SettingsSections.StorageAndPerformance)
            ];
            using SettingsViewModel viewModel = CreateSettingsViewModel(settings);
            SettingsOverlayView view = new() { DataContext = viewModel };
            view.DataTemplates.Add(new FuncDataTemplate<ISettingItemViewModel>(
                (setting, _) => new TextBlock { Classes = { "test-editor" }, Text = setting?.Key }));

            Window window = Show(view, width, height);
            try
            {
                TabStrip tabs = view.FindControl<TabStrip>("SectionTabs")
                    ?? throw new InvalidOperationException("Settings tabs were not found.");
                TransitioningContentControl content = view.FindControl<TransitioningContentControl>("SectionContent")
                    ?? throw new InvalidOperationException("Settings content was not found.");
                TabStripItem[] tabItems = tabs.GetVisualDescendants().OfType<TabStripItem>().ToArray();
                Rect[] tabBounds = tabItems.Select(item => item.Bounds).ToArray();
                ModalOverlayControl panel = view.GetVisualDescendants().OfType<ModalOverlayControl>().Single();

                tabs.SelectedIndex.Should().Be(0);
                panel.Bounds.Width.Should().Be(panelWidth);
                content.PageTransition.Should().BeOfType<CrossFade>()
                    .Subject.Duration.Should().Be(TimeSpan.FromMilliseconds(108));
                tabItems.Select(item => item.DataContext).Should().Equal(viewModel.Groups);
                AssertActiveTabMarker(tabItems[0]);
                AssertRenderedSetting(view, settings[0]);

                for (int index = 1; index < tabItems.Length; index++)
                {
                    TaskCompletionSource<TransitionCompletedEventArgs> completion = new();
                    void OnTransitionCompleted(object? sender, TransitionCompletedEventArgs args)
                    {
                        if (ReferenceEquals(args.To, tabItems[index].DataContext))
                        {
                            completion.TrySetResult(args);
                        }
                    }

                    content.TransitionCompleted += OnTransitionCompleted;
                    ClickTab(window, tabItems[index]);
                    TransitionCompletedEventArgs transition = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
                    content.TransitionCompleted -= OnTransitionCompleted;
                    window.CaptureRenderedFrame();

                    transition.HasRunToCompletion.Should().BeTrue();
                    viewModel.SelectedGroup.Should().BeSameAs(viewModel.Groups[index]);
                    tabItems.Select(item => item.Bounds).Should().Equal(tabBounds);
                    AssertActiveTabMarker(tabItems[index]);
                    AssertRenderedSetting(view, settings[index]);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ScrollViewer_WhenWindowShrinks_ReachesBottomOfSettingsPanel()
    {
        await DispatchAsync(() =>
        {
            using SettingsViewModel viewModel = CreateSettingsViewModel([CreateSetting(SettingsSections.Connection)]);
            SettingsOverlayView view = new() { DataContext = viewModel };
            view.DataTemplates.Add(new FuncDataTemplate<ISettingItemViewModel>(
                (_, _) => new Border { Height = 420d }));

            Show(view, 670d, 768d, window =>
            {
                ModalOverlayControl panel = view.GetVisualDescendants().OfType<ModalOverlayControl>().Single();
                ScrollViewer scroll = view.GetVisualDescendants().OfType<ScrollViewer>()
                    .Single(control => control.Classes.Contains("settings-section-scroll") && control.IsEffectivelyVisible);
                ScrollContentPresenter viewport = scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().Single();
                Border content = scroll.Content.Should().BeOfType<Border>().Subject;

                window.Height = 340d;
                window.CaptureRenderedFrame();
                Point wheelPoint = viewport.TranslatePoint(
                    new Point(viewport.Bounds.Width / 2d, viewport.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The settings viewport is not attached.");
                SmoothScrollBehavior.SetDuration(scroll, TimeSpan.Zero);
                window.MouseWheel(wheelPoint, new Vector(0d, -20d), RawInputModifiers.None);
                window.CaptureRenderedFrame();

                scroll.Offset.Y.Should().BeGreaterThan(0d);
                scroll.Offset.Y.Should().BeApproximately(scroll.Extent.Height - scroll.Viewport.Height, 1d);
                (content.TranslatePoint(new Point(0d, content.Bounds.Height), viewport)?.Y)
                    .Should().BeApproximately(viewport.Bounds.Height, 1d);
                (viewport.TranslatePoint(new Point(0d, viewport.Bounds.Height), panel)?.Y)
                    .Should().BeApproximately(panel.Bounds.Height, 1d);
            });
        });
    }

    [Fact]
    public async Task Connection_WhenShown_SeparatesOnlyAdjacentGroups()
    {
        await DispatchAsync(() =>
        {
            using SettingsViewModel viewModel = CreateSettingsViewModel(
                [CreateSetting(SettingsSections.Server), CreateSetting(SettingsSections.Google), CreateSetting(SettingsSections.OpenRouter)]);
            SettingsOverlayView view = new() { DataContext = viewModel };

            Show(view, 1024d, 768d, _ =>
            {
                view.GetVisualDescendants().OfType<Border>()
                    .Where(border => border.Classes.Contains("settings-row"))
                    .Select(border => border.BorderThickness)
                    .Should().Equal(default, new Thickness(0d, 1d, 0d, 0d), new Thickness(0d, 1d, 0d, 0d));
            });
        });
    }

    [Fact]
    public async Task Sections_WhenLeavingEditedCredential_SavesPendingValue()
    {
        await DispatchAsync(() =>
        {
            const string EditedValue = "edited-value-for-test-only";
            Mock<ISecretStore> store = new();
            store.Setup(secretStore => secretStore.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("stored-value-for-test-only");
            store.Setup(secretStore => secretStore.SetSecretAsync(
                    GoogleApiKeySettingDefinition.SecretNameValue, EditedValue, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            SecretSettingViewModel credential = new(
                new GoogleApiKeySettingDefinition(), store.Object,
                new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);
            using SettingsViewModel viewModel = CreateSettingsViewModel([credential, CreateSetting(SettingsSections.Models)]);
            SettingsOverlayView view = new() { DataContext = viewModel };
            view.DataTemplates.Add(new FuncDataTemplate<SecretSettingViewModel>((_, _) => new SecretSettingView()));

            Show(view, 1024d, 768d, window =>
            {
                TextBox input = view.GetVisualDescendants().OfType<TextBox>().Single();
                TabStripItem modelsTab = view.GetVisualDescendants().OfType<TabStripItem>().Last();
                input.Focus().Should().BeTrue();
                input.Text = EditedValue;

                ClickTab(window, modelsTab);

                viewModel.SelectedGroup.Should().BeSameAs(viewModel.Groups[1]);
                store.Verify(secretStore => secretStore.SetSecretAsync(
                    GoogleApiKeySettingDefinition.SecretNameValue, EditedValue, It.IsAny<CancellationToken>()), Times.Once);
            });
        });
    }

    private static SettingsViewModel CreateSettingsViewModel(IReadOnlyList<ISettingItemViewModel> settings)
    {
        Mock<ISettingsItemViewModelProvider> provider = new();
        provider.Setup(source => source.CreateSettings()).Returns(settings);
        return new SettingsViewModel(
            provider.Object, TestLocalizationTextProvider.Default, new WeakReferenceMessenger());
    }

    private static ISettingItemViewModel CreateSetting(SettingsSection section)
    {
        Mock<ISettingItemViewModel> setting = new();
        setting.SetupGet(item => item.Section).Returns(section);
        setting.SetupGet(item => item.Key).Returns(section.Key);
        return setting.Object;
    }

    private static TextBlock GetVisibleSettingLabel(Control view)
    {
        return view.GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Classes.Contains("settings-label") && text.IsEffectivelyVisible);
    }

    private static Border GetSwitchTrack(Control view)
    {
        return view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "SwitchTrack");
    }

    private static void AssertRenderedSetting(SettingsOverlayView view, ISettingItemViewModel setting)
    {
        view.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.Classes.Contains("test-editor") && text.IsEffectivelyVisible)
            .Select(text => text.Text)
            .Should().Equal(setting.Key);
    }

    private static void AssertActiveTabMarker(TabStripItem tab)
    {
        Border marker = tab.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "TabMarker");
        BrushTransition transition = marker.Transitions.Should().ContainSingle().Subject
            .Should().BeOfType<BrushTransition>().Subject;
        transition.Duration.Should().Be(TimeSpan.FromMilliseconds(108));
        transition.Property.Should().Be(Border.BorderBrushProperty);
        marker.BorderBrush.Should().BeSameAs(tab.FindResource("MetadataRepeatBrush"));
    }

    private static void ClickTab(Window window, TabStripItem tab)
    {
        Point point = tab.TranslatePoint(new Point(tab.Bounds.Width / 2d, tab.Bounds.Height / 2d), window)
            ?? throw new InvalidOperationException("Settings tab is not attached.");
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.CaptureRenderedFrame();
    }
}
