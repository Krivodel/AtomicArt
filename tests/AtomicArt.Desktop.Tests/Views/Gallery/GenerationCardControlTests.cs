using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using Rectangle = Avalonia.Controls.Shapes.Rectangle;

using CommunityToolkit.Mvvm.Input;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GenerationCardControlTests : DesktopControlTestBase
{
    private const string Prompt = "Prompt";
    private const string AspectRatio = GenerationAspectRatios.Auto;

    private static readonly Guid ItemId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Size PreviewSize = new(220d, 220d);
    private static readonly Rect DefaultViewportBounds = new(0d, 0d, 1000d, 600d);
    private static readonly TimeSpan AnimationCompletionTimeout = TimeSpan.FromSeconds(1d);

    [Theory]
    [InlineData(KeyModifiers.None, false)]
    [InlineData(KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Control, true)]
    [InlineData(KeyModifiers.Alt, true)]
    [InlineData(KeyModifiers.Meta, false)]
    [InlineData(KeyModifiers.Shift | KeyModifiers.Control, true)]
    public void HasExpansionModifier_WithKeyModifiers_DetectsSupportedModifier(
        KeyModifiers modifiers,
        bool expectedResult)
    {
        bool result = GenerationPreviewExpansionController.HasExpansionModifier(modifiers);

        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(KeyModifiers.None, false)]
    [InlineData(KeyModifiers.Meta, false)]
    [InlineData(KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Control, true)]
    [InlineData(KeyModifiers.Alt, true)]
    public void ResolveFileRevealCommand_WithModifiers_SelectsExpectedCommand(
        KeyModifiers modifiers,
        bool expectsNewWindowCommand)
    {
        RelayCommand defaultCommand = new(() => { });
        RelayCommand newWindowCommand = new(() => { });

        IRelayCommand? result = GenerationCardControl.ResolveFileRevealCommand(
            modifiers,
            defaultCommand,
            newWindowCommand);

        result.Should().BeSameAs(
            expectsNewWindowCommand ? newWindowCommand : defaultCommand);
    }

    [Theory]
    [InlineData(KeyModifiers.None, false)]
    [InlineData(KeyModifiers.Control, false)]
    [InlineData(KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Shift | KeyModifiers.Control, true)]
    public void ResolveSelectionCommand_WithModifiers_SelectsExpectedCommand(
        KeyModifiers modifiers,
        bool expectsRangeCommand)
    {
        RelayCommand toggleCommand = new(() => { });
        RelayCommand rangeCommand = new(() => { });

        IRelayCommand? result = GenerationCardControl.ResolveSelectionCommand(
            modifiers,
            toggleCommand,
            rangeCommand);

        result.Should().BeSameAs(
            expectsRangeCommand ? rangeCommand : toggleCommand);
    }

    [Fact]
    public void Calculate_WithWideSource_ScalesFullAspectRatioAndFitsRightViewportEdge()
    {
        AssertExpansion(
            new Size(440d, 220d),
            new Rect(780d, 40d, 220d, 220d),
            new Size(748d, 374d),
            new Vector(-528d, -40d));
    }

    [Fact]
    public void Calculate_WithTallSource_ScalesFullAspectRatioAndFitsViewportStart()
    {
        AssertExpansion(
            new Size(220d, 440d),
            new Rect(40d, 380d, 220d, 220d),
            new Size(374d, 748d),
            new Vector(-40d, -380d));
    }

    [Fact]
    public void Calculate_WithCenteredWideSource_ExpandsEvenlyAroundPreview()
    {
        AssertExpansion(
            new Size(330d, 220d),
            new Rect(390d, 40d, 220d, 220d),
            new Size(561d, 374d),
            new Vector(-170.5d, -40d));
    }

    [Fact]
    public void Calculate_WithOffsetViewport_FitsExpandedPreviewInsideActualVisibleBounds()
    {
        Size sourceSize = new(440d, 220d);
        Rect previewBounds = new(780d, 40d, 220d, 220d);
        Rect viewportBounds = new(20d, 0d, 960d, 600d);

        (Size size, Vector translation) = Calculate(
            sourceSize,
            previewBounds,
            viewportBounds);

        size.Should().Be(new Size(748d, 374d));
        translation.Should().Be(new Vector(-548d, -40d));
        (previewBounds.Left + translation.X + size.Width).Should().Be(viewportBounds.Right);
    }

    [Fact]
    public void GetImageDragPathOrDefault_WithExistingFullImageAndThumbnail_ReturnsFullImagePath()
    {
        using ExistingImagePaths paths = new();

        string? dragPath = GenerationCardControl.GetImageDragPathOrDefault(paths.Item);

        dragPath.Should().Be(paths.ImagePath);
    }

    [Fact]
    public void GetImageDragPathOrDefault_WithMissingFullImageAndExistingThumbnail_ReturnsNull()
    {
        string imagePath = Path.Combine(Path.GetTempPath(), "atomic-art-missing-generation-card-drag-test.png");
        string thumbnailPath = Path.GetTempFileName();

        try
        {
            File.Delete(imagePath);
            GenerationItemViewModel item = CreateItem(imagePath, thumbnailPath);

            string? dragPath = GenerationCardControl.GetImageDragPathOrDefault(item);

            dragPath.Should().BeNull();
        }
        finally
        {
            File.Delete(thumbnailPath);
        }
    }

    [Fact]
    public void GetImageDragPreviewPathOrDefault_WithExistingThumbnail_ReturnsThumbnailPath()
    {
        using ExistingImagePaths paths = new();

        string? previewPath = GenerationCardControl.GetImageDragPreviewPathOrDefault(paths.Item);

        previewPath.Should().Be(paths.ThumbnailPath);
    }

    [Fact]
    public void GetImageDragPreviewPathOrDefault_WithMissingThumbnail_ReturnsFullImagePath()
    {
        string imagePath = Path.GetTempFileName();
        string thumbnailPath = Path.Combine(Path.GetTempPath(), "atomic-art-missing-generation-card-preview-test.png");

        try
        {
            File.Delete(thumbnailPath);
            GenerationItemViewModel item = CreateItem(imagePath, thumbnailPath);

            string? previewPath = GenerationCardControl.GetImageDragPreviewPathOrDefault(item);

            previewPath.Should().Be(imagePath);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task PromptArea_WhenClicked_OpensMetadataAsync()
    {
        await DispatchAsync(() =>
        {
            bool isMetadataOpened = false;
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            RelayCommand command = new(() => isMetadataOpened = true);
            GenerationCardControl control = new()
            {
                DataContext = item,
                OpenMetadataCommand = command
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                Point promptAreaCenter = new(110d, 270d);

                window.MouseDown(promptAreaCenter, MouseButton.Left);
                window.MouseUp(promptAreaCenter, MouseButton.Left);

                isMetadataOpened.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void ContextFlyout_WhenCardCreated_ContainsExpectedItemsAndIcons()
    {
        Dispatch(() =>
        {
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            RelayCommand selectCommand = new(() => item.IsSelected = true);
            RelayCommand revealCommand = new(() => { });
            RelayCommand deleteCommand = new(() => { });
            GenerationCardControl control = new()
            {
                DataContext = item,
                DeleteOrCancelCommand = deleteCommand,
                RevealInFolderCommand = revealCommand,
                ToggleSelectionCommand = selectCommand
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                Border cardContainer = control
                    .FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException(
                        "Generation card container was not found.");
                AnimatedContextMenuFlyout menuFlyout = cardContainer.ContextFlyout
                    .Should()
                    .BeOfType<AnimatedContextMenuFlyout>()
                    .Subject;
                MenuItem selectMenuItem = control
                    .FindControl<MenuItem>("SelectMenuItem")
                    ?? throw new InvalidOperationException(
                        "Select menu item was not found.");
                MenuItem showInFolderMenuItem = control
                    .FindControl<MenuItem>("ShowInFolderMenuItem")
                    ?? throw new InvalidOperationException(
                        "Show-in-folder menu item was not found.");
                MenuItem imbaMenuItem = control
                    .FindControl<MenuItem>("ImbaMenuItem")
                    ?? throw new InvalidOperationException(
                        "IMBA menu item was not found.");
                MenuItem deleteMenuItem = control
                    .FindControl<MenuItem>("DeleteMenuItem")
                    ?? throw new InvalidOperationException(
                        "Delete menu item was not found.");

                menuFlyout.Items.Should().HaveCount(4);
                menuFlyout.Items[0].Should().BeSameAs(selectMenuItem);
                menuFlyout.Items[3].Should().BeSameAs(deleteMenuItem);
                Avalonia.Controls.Shapes.Path selectIcon = selectMenuItem.Icon
                    .Should()
                    .BeOfType<Avalonia.Controls.Shapes.Path>()
                    .Subject;
                selectIcon.Fill.Should().BeNull();
                selectIcon.Stroke.Should().NotBeNull();
                selectIcon.Effect.Should().BeNull();
                selectMenuItem.Command.Should().BeSameAs(selectCommand);
                selectMenuItem.CommandParameter.Should().BeSameAs(item);
                selectMenuItem.IsEnabled.Should().BeTrue();
                showInFolderMenuItem.Icon.Should().BeOfType<PathIcon>();
                showInFolderMenuItem.Command.Should().BeSameAs(revealCommand);
                showInFolderMenuItem.CommandParameter.Should().BeSameAs(item);
                imbaMenuItem.Icon.Should().BeOfType<PathIcon>();
                imbaMenuItem.IsEnabled.Should().BeTrue();
                imbaMenuItem.Command.Should().BeNull();
                PathIcon deleteIcon = deleteMenuItem.Icon
                    .Should()
                    .BeOfType<PathIcon>()
                    .Subject;
                deleteIcon.TryFindResource(
                    "SukiDangerColor",
                    out object? dangerResource).Should().BeTrue();
                Color dangerColor = dangerResource
                    .Should()
                    .BeOfType<Color>()
                    .Subject;
                deleteIcon.Foreground
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Which.Color.Should().Be(dangerColor);
                deleteMenuItem.Command.Should().BeSameAs(deleteCommand);
                deleteMenuItem.CommandParameter.Should().BeSameAs(item);
                menuFlyout.Popup.WindowManagerAddShadowHint.Should().BeFalse();

                item.IsSelected = true;

                selectMenuItem.IsEnabled.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ContextFlyout_WhenCardRightClicked_OpensWithSnapshotRevealAsync()
    {
        await DispatchAsync(async () =>
        {
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            GenerationCardControl control = new()
            {
                DataContext = item
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                Border cardContainer = control
                    .FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException(
                        "Generation card container was not found.");
                AnimatedContextMenuFlyout menuFlyout = cardContainer.ContextFlyout
                    .Should()
                    .BeOfType<AnimatedContextMenuFlyout>()
                    .Subject;
                Point cardCenter = new(
                    GalleryLayoutService.CardWidth / 2d,
                    GalleryLayoutService.CardHeight / 2d);

                window.MouseDown(cardCenter, MouseButton.Right);
                window.MouseUp(cardCenter, MouseButton.Right);
                window.CaptureRenderedFrame();

                MenuItem selectMenuItem = control
                    .FindControl<MenuItem>("SelectMenuItem")
                    ?? throw new InvalidOperationException(
                        "Select menu item was not found.");
                MenuFlyoutPresenter presenter = selectMenuItem
                    .GetVisualAncestors()
                    .OfType<MenuFlyoutPresenter>()
                    .Single();
                MenuItem[] menuItems = presenter
                    .GetVisualDescendants()
                    .OfType<MenuItem>()
                    .ToArray();
                Panel presenterTemplateRoot = presenter
                    .GetVisualChildren()
                    .OfType<Panel>()
                    .Single();
                Border[] presenterChromeBorders = presenterTemplateRoot
                    .GetVisualChildren()
                    .OfType<Border>()
                    .ToArray();
                Rectangle[] iconSeparators = presenter
                    .GetVisualDescendants()
                    .OfType<Rectangle>()
                    .Where(rectangle => string.Equals(
                        rectangle.Name,
                        "PART_HorizontalSeparator",
                        StringComparison.Ordinal))
                    .ToArray();
                ContentPresenter[] iconPresenters = presenter
                    .GetVisualDescendants()
                    .OfType<ContentPresenter>()
                    .Where(contentPresenter => string.Equals(
                        contentPresenter.Name,
                        "PART_IconPresenter",
                        StringComparison.Ordinal))
                    .ToArray();
                ContentPresenter[] headerPresenters = presenter
                    .GetVisualDescendants()
                    .OfType<ContentPresenter>()
                    .Where(contentPresenter => string.Equals(
                        contentPresenter.Name,
                        "PART_HeaderPresenter",
                        StringComparison.Ordinal))
                    .ToArray();
                TextBlock[] menuHeaderTextBlocks = menuItems
                    .Select(menuItem => menuItem.Header)
                    .OfType<TextBlock>()
                    .ToArray();
                ContextMenuRevealHost revealHost = presenter
                    .GetVisualAncestors()
                    .OfType<ContextMenuRevealHost>()
                    .Single();
                RenderTargetBitmap snapshot = revealHost.Snapshot
                    ?? throw new InvalidOperationException(
                        "Context menu snapshot was not created.");
                bool backgroundFound = presenter.TryFindResource(
                    "ContextMenuBackgroundBrush",
                    out object? backgroundResource);
                LinearGradientBrush backgroundBrush = presenter.Background
                    .Should()
                    .BeOfType<LinearGradientBrush>()
                    .Subject;
                menuFlyout.IsOpen.Should().BeTrue();
                menuItems.Should().HaveCount(4);
                menuItems[0].Should().BeSameAs(selectMenuItem);
                selectMenuItem.IsSelected.Should().BeFalse();
                selectMenuItem.IsPointerOver.Should().BeFalse();
                selectMenuItem.IsFocused.Should().BeFalse();
                presenter.Focusable.Should().BeTrue();
                presenter.IsFocused.Should().BeTrue();
                presenterTemplateRoot.Margin.Should().Be(default(Thickness));
                presenterChromeBorders.Should().HaveCount(2);
                presenterChromeBorders.Should().OnlyContain(
                    border => border.Margin == default);
                presenterChromeBorders[0].IsVisible.Should().BeFalse();
                presenterChromeBorders[1].IsVisible.Should().BeTrue();
                iconSeparators.Should().HaveCount(4);
                iconSeparators.Should().OnlyContain(separator => separator.Opacity == 0d);
                iconPresenters.Should().HaveCount(4);
                headerPresenters.Should().HaveCount(4);
                menuHeaderTextBlocks.Should().HaveCount(4);
                menuHeaderTextBlocks.Should().OnlyContain(
                    textBlock => textBlock.FontWeight == FontWeight.Normal);

                for (int index = 0; index < iconPresenters.Length; index++)
                {
                    TranslateTransform translateTransform = iconPresenters[index]
                        .RenderTransform
                        .Should()
                        .BeOfType<TranslateTransform>()
                        .Subject;
                    double expectedOffset = index == 0 ? 8d : 4d;
                    translateTransform.X.Should().Be(expectedOffset);
                }

                TranslateTransform selectHeaderTransform = headerPresenters[0]
                    .RenderTransform
                    .Should()
                    .BeOfType<TranslateTransform>()
                    .Subject;
                selectHeaderTransform.X.Should().Be(4d);

                foreach (ContentPresenter headerPresenter in headerPresenters.Skip(1))
                {
                    headerPresenter.RenderTransform.Should().BeNull();
                }

                presenter.RenderTransform.Should().BeNull();
                presenter.Opacity.Should().Be(0d);
                presenter.IsHitTestVisible.Should().BeTrue();
                backgroundFound.Should().BeTrue();
                presenter.Background.Should().BeSameAs(backgroundResource);
                backgroundBrush.GradientStops.Should().HaveCount(2);
                backgroundBrush.GradientStops[0].Color.Should().NotBe(
                    backgroundBrush.GradientStops[1].Color);
                presenter.BorderThickness.Should().Be(default(Thickness));
                presenter.CornerRadius.Should().Be(new CornerRadius(8d));
                menuItems.Should().OnlyContain(
                    menuItem => Math.Abs(
                        menuItem.Bounds.Width - presenter.Bounds.Width) < 0.001d);
                menuItems[0].Bounds.Top.Should().Be(0d);
                menuItems[^1].Bounds.Bottom.Should().BeApproximately(
                    presenter.Bounds.Height,
                    0.001d);
                revealHost.WidthRatio.Should().BeInRange(
                    ContextMenuRevealHost.InitialWidthRatio,
                    1d);
                revealHost.HeightRatio.Should().BeInRange(
                    ContextMenuRevealHost.InitialHeightRatio,
                    1d);
                revealHost.RevealBounds.Width.Should().BeApproximately(
                    presenter.Bounds.Width * revealHost.WidthRatio,
                    0.001d);
                revealHost.RevealBounds.Height.Should().BeApproximately(
                    presenter.Bounds.Height * revealHost.HeightRatio,
                    0.001d);
                snapshot.Size.Width.Should().BeApproximately(
                    presenter.Bounds.Width,
                    0.5d);
                snapshot.Size.Height.Should().BeApproximately(
                    presenter.Bounds.Height,
                    0.5d);
                revealHost.BoxShadows.Should().NotBe(default(BoxShadows));
                revealHost.Padding.Left.Should().BeGreaterThan(0d);
                revealHost.Padding.Top.Should().BeGreaterThan(0d);
                revealHost.Padding.Right.Should().BeGreaterThan(0d);
                revealHost.Padding.Bottom.Should().BeGreaterThan(0d);

                await Task.Delay(
                    ContextMenuRevealHost.OpeningDurationMilliseconds + 50);

                iconSeparators.Should().OnlyContain(separator => separator.Opacity == 0d);
                revealHost.Snapshot.Should().BeNull();

                menuFlyout.Hide();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(0.6d)]
    [InlineData(1.5d)]
    public async Task ContextFlyout_WhenCardIsScaled_InheritsUiScaleAsync(
        double uiScale)
    {
        await DispatchAsync(() =>
        {
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            GenerationCardControl control = new()
            {
                DataContext = item
            };
            LayoutTransformControl scaleHost = new()
            {
                Child = control,
                LayoutTransform = new ScaleTransform(uiScale, uiScale)
            };
            Window window = Show(
                scaleHost,
                GalleryLayoutService.CardWidth * uiScale,
                GalleryLayoutService.CardHeight * uiScale);

            try
            {
                Border cardContainer = control
                    .FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException(
                        "Generation card container was not found.");
                AnimatedContextMenuFlyout menuFlyout = cardContainer.ContextFlyout
                    .Should()
                    .BeOfType<AnimatedContextMenuFlyout>()
                    .Subject;
                Point cardCenter = cardContainer.TranslatePoint(
                        new Point(
                            cardContainer.Bounds.Width / 2d,
                            cardContainer.Bounds.Height / 2d),
                        window)
                    ?? throw new InvalidOperationException(
                        "Generation card position was not found.");

                window.MouseDown(cardCenter, MouseButton.Right);
                window.MouseUp(cardCenter, MouseButton.Right);
                StartContextMenuOpening(window);

                ContextMenuRevealHost revealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;
                PopupAssertions.AssertInheritsScale(
                    menuFlyout.Popup,
                    revealHost,
                    uiScale);

                menuFlyout.Hide();
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void ContextFlyout_WhenDownPressed_SelectsFirstItem()
    {
        Dispatch(() =>
        {
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            GenerationCardControl control = new()
            {
                DataContext = item
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                Border cardContainer = control
                    .FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException(
                        "Generation card container was not found.");
                AnimatedContextMenuFlyout menuFlyout = cardContainer.ContextFlyout
                    .Should()
                    .BeOfType<AnimatedContextMenuFlyout>()
                    .Subject;
                Point cardCenter = new(
                    GalleryLayoutService.CardWidth / 2d,
                    GalleryLayoutService.CardHeight / 2d);
                window.MouseDown(cardCenter, MouseButton.Right);
                window.MouseUp(cardCenter, MouseButton.Right);
                StartContextMenuOpening(window);
                ContextMenuRevealHost revealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;
                MenuItem showInFolderMenuItem = control
                    .FindControl<MenuItem>("ShowInFolderMenuItem")
                    ?? throw new InvalidOperationException(
                        "Show-in-folder menu item was not found.");
                TopLevel popupRoot = TopLevel.GetTopLevel(revealHost)
                    ?? throw new InvalidOperationException(
                        "Context menu popup root was not found.");

                popupRoot.KeyPress(
                    Key.Down,
                    RawInputModifiers.None,
                    PhysicalKey.None,
                    null);

                showInFolderMenuItem.IsSelected.Should().BeTrue();
                showInFolderMenuItem.IsFocused.Should().BeTrue();

                menuFlyout.Hide();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ContextFlyout_WhenItemClickedDuringOpening_ExecutesCommandImmediately()
    {
        Dispatch(() =>
        {
            bool commandExecuted = false;
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            RelayCommand revealCommand = new(() => commandExecuted = true);
            GenerationCardControl control = new()
            {
                DataContext = item,
                RevealInFolderCommand = revealCommand
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                Border cardContainer = control
                    .FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException(
                        "Generation card container was not found.");
                AnimatedContextMenuFlyout menuFlyout = cardContainer.ContextFlyout
                    .Should()
                    .BeOfType<AnimatedContextMenuFlyout>()
                    .Subject;
                int closingCount = 0;
                menuFlyout.Closing += (_, _) => closingCount++;
                Point cardCenter = new(
                    GalleryLayoutService.CardWidth / 2d,
                    GalleryLayoutService.CardHeight / 2d);
                window.MouseDown(cardCenter, MouseButton.Right);
                window.MouseUp(cardCenter, MouseButton.Right);
                StartContextMenuOpening(window);
                ContextMenuRevealHost revealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;
                MenuItem showInFolderMenuItem = control
                    .FindControl<MenuItem>("ShowInFolderMenuItem")
                    ?? throw new InvalidOperationException(
                        "Show-in-folder menu item was not found.");
                _ = revealHost.Snapshot
                    ?? throw new InvalidOperationException(
                        "Context menu opening animation was not running.");

                ClickMenuItem(showInFolderMenuItem, revealHost);

                commandExecuted.Should().BeTrue();
                closingCount.Should().Be(1);
                menuFlyout.IsOpen.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ContextFlyout_WhenHidden_FadesBeforeClosingAsync()
    {
        await DispatchAsync(async () =>
        {
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            GenerationCardControl control = new()
            {
                DataContext = item
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                Border cardContainer = control
                    .FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException(
                        "Generation card container was not found.");
                AnimatedContextMenuFlyout menuFlyout = cardContainer.ContextFlyout
                    .Should()
                    .BeOfType<AnimatedContextMenuFlyout>()
                    .Subject;
                TaskCompletionSource<bool> closed = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                int closingCount = 0;
                menuFlyout.Closing += (_, _) => closingCount++;
                menuFlyout.Closed += (_, _) => closed.TrySetResult(true);
                Point cardCenter = new(
                    GalleryLayoutService.CardWidth / 2d,
                    GalleryLayoutService.CardHeight / 2d);
                window.MouseDown(cardCenter, MouseButton.Right);
                window.MouseUp(cardCenter, MouseButton.Right);
                ContextMenuRevealHost revealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;
                await Task.Delay(
                    ContextMenuRevealHost.OpeningDurationMilliseconds + 50);

                revealHost.Opacity.Should().Be(1d);
                revealHost.Snapshot.Should().BeNull();
                revealHost.BoxShadows.Should().NotBe(default(BoxShadows));

                menuFlyout.Hide();

                menuFlyout.IsOpen.Should().BeTrue();
                Task completedTask = await Task.WhenAny(
                    closed.Task,
                    Task.Delay(AnimationCompletionTimeout));
                completedTask.Should().BeSameAs(closed.Task);
                menuFlyout.IsOpen.Should().BeFalse();
                closingCount.Should().Be(1);
                menuFlyout.Popup.Child.Should().BeOfType<MenuFlyoutPresenter>();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ContextFlyout_WhenOpenedAfterEarlyItemClick_IsVisibleAgainAsync()
    {
        await DispatchAsync(async () =>
        {
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            GenerationCardControl control = new()
            {
                DataContext = item,
                RevealInFolderCommand = new RelayCommand(() => { })
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                Border cardContainer = control
                    .FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException(
                        "Generation card container was not found.");
                AnimatedContextMenuFlyout menuFlyout = cardContainer.ContextFlyout
                    .Should()
                    .BeOfType<AnimatedContextMenuFlyout>()
                    .Subject;
                Point cardCenter = new(
                    GalleryLayoutService.CardWidth / 2d,
                    GalleryLayoutService.CardHeight / 2d);
                window.MouseDown(cardCenter, MouseButton.Right);
                window.MouseUp(cardCenter, MouseButton.Right);
                StartContextMenuOpening(window);
                ContextMenuRevealHost firstRevealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;
                MenuItem showInFolderMenuItem = control
                    .FindControl<MenuItem>("ShowInFolderMenuItem")
                    ?? throw new InvalidOperationException(
                        "Show-in-folder menu item was not found.");
                _ = firstRevealHost.Snapshot
                    ?? throw new InvalidOperationException(
                        "Context menu opening animation was not running.");
                TaskCompletionSource<bool> firstClose = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                menuFlyout.Closed += OnFirstClosed;

                ClickMenuItem(showInFolderMenuItem, firstRevealHost);

                Task firstCompletedTask = await Task.WhenAny(
                    firstClose.Task,
                    Task.Delay(AnimationCompletionTimeout));
                firstCompletedTask.Should().BeSameAs(firstClose.Task);
                menuFlyout.Closed -= OnFirstClosed;
                window.MouseDown(cardCenter, MouseButton.Right);
                window.MouseUp(cardCenter, MouseButton.Right);
                StartContextMenuOpening(window);
                ContextMenuRevealHost secondRevealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;

                menuFlyout.IsOpen.Should().BeTrue();
                secondRevealHost.Snapshot.Should().NotBeNull();
                await Task.Delay(
                    ContextMenuRevealHost.OpeningDurationMilliseconds + 50);

                MenuFlyoutPresenter secondPresenter = secondRevealHost.Child
                    .Should()
                    .BeOfType<MenuFlyoutPresenter>()
                    .Subject;
                secondRevealHost.Opacity.Should().Be(1d);
                secondRevealHost.Snapshot.Should().BeNull();
                secondPresenter.Opacity.Should().Be(1d);
                secondPresenter.IsHitTestVisible.Should().BeTrue();
                TaskCompletionSource<bool> secondClose = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                menuFlyout.Closed += OnSecondClosed;

                menuFlyout.Hide();

                Task secondCompletedTask = await Task.WhenAny(
                    secondClose.Task,
                    Task.Delay(AnimationCompletionTimeout));
                secondCompletedTask.Should().BeSameAs(secondClose.Task);
                menuFlyout.Closed -= OnSecondClosed;

                void OnFirstClosed(object? sender, EventArgs eventArgs)
                {
                    firstClose.TrySetResult(true);
                }

                void OnSecondClosed(object? sender, EventArgs eventArgs)
                {
                    secondClose.TrySetResult(true);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(
        "Сделай первую картинку (игра 3 в ряд, 2д) в стиле второй " +
        "картинки (игра borderlands 3). Не меняй игру на 1 картинке, " +
        "нужны лишь другие текстуры/рисовка/фон.")]
    [InlineData(
        "Первая строка\n" +
        "Вторая строка\n" +
        "Третья строка\n" +
        "Скрытая четвёртая строка")]
    [InlineData(
        "ОченьДлинныйНеразрывныйПромптОченьДлинныйНеразрывныйПромпт" +
        "ОченьДлинныйНеразрывныйПромптОченьДлинныйНеразрывныйПромпт" +
        "ОченьДлинныйНеразрывныйПромптОченьДлинныйНеразрывныйПромпт")]
    public async Task Prompt_WhenReusedCardTextOverflows_EndsWithEllipsisAsync(
        string overflowingPrompt)
    {
        await DispatchAsync(() =>
        {
            const string Ellipsis = "…";
            GenerationItemViewModel initialItem = CreateItem(
                "initial-image.png",
                "initial-thumbnail.jpg");
            GenerationItemViewModel overflowingItem = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg",
                overflowingPrompt);
            GenerationCardControl control = new()
            {
                DataContext = initialItem
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                TextBlock initialPrompt = GetPromptTextBlock(control, Prompt);
                GetRenderedText(initialPrompt).Should().Be(Prompt);

                control.DataContext = overflowingItem;
                Size cardSize = new(
                    GalleryLayoutService.CardWidth,
                    GalleryLayoutService.CardHeight);
                control.Measure(cardSize);
                control.Arrange(new Rect(cardSize));

                OverflowEllipsisTextBlock prompt = GetPromptTextBlock(
                        control,
                        overflowingPrompt)
                    .Should()
                    .BeOfType<OverflowEllipsisTextBlock>()
                    .Subject;
                prompt.TextTrimming.Should().NotBe(TextTrimming.None);
                prompt.TextLayout.TextLines.Should().HaveCount(prompt.MaxLines);
                string renderedText = GetRenderedText(prompt);
                renderedText.Should().EndWith(Ellipsis);
                renderedText.Should().NotEndWith(Ellipsis + Ellipsis);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Prompt_WhenCardShown_MatchesStandardTextAppearanceAsync()
    {
        await DispatchAsync(() =>
        {
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            GenerationCardControl control = new()
            {
                DataContext = item
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                TextBlock prompt = GetPromptTextBlock(control, Prompt);
                TextBlock model = GetPromptTextBlock(
                    control,
                    item.ModelDisplayName);

                prompt.FontFamily.Should().Be(model.FontFamily);
                prompt.FontSize.Should().Be(model.FontSize);
                prompt.FontStretch.Should().Be(model.FontStretch);
                prompt.FontStyle.Should().Be(model.FontStyle);
                prompt.Foreground.Should().Be(model.Foreground);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    private static TextBlock GetPromptTextBlock(
        GenerationCardControl control,
        string prompt)
    {
        return control
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(textBlock => string.Equals(
                textBlock.Text,
                prompt,
                StringComparison.Ordinal));
    }

    private static string GetRenderedText(TextBlock textBlock)
    {
        return string.Concat(
            textBlock.TextLayout.TextLines
                .SelectMany(line => line.TextRuns)
                .Select(run => run.Text.ToString()));
    }

    private static void ClickMenuItem(
        MenuItem menuItem,
        ContextMenuRevealHost revealHost)
    {
        TopLevel popupRoot = TopLevel.GetTopLevel(revealHost)
            ?? throw new InvalidOperationException(
                "Context menu popup root was not found.");
        Point menuItemCenter = menuItem.TranslatePoint(
                new Point(
                    menuItem.Bounds.Width / 2d,
                    menuItem.Bounds.Height / 2d),
                popupRoot)
            ?? throw new InvalidOperationException(
                "Context menu item position was not found.");

        popupRoot.MouseDown(menuItemCenter, MouseButton.Left);
        popupRoot.MouseUp(menuItemCenter, MouseButton.Left);
    }

    private static void StartContextMenuOpening(Window window)
    {
        window.CaptureRenderedFrame();
    }

    private static GenerationItemViewModel CreateItem(
        string imagePath,
        string thumbnailPath,
        string prompt = Prompt)
    {
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            prompt: prompt,
            aspectRatio: AspectRatio,
            createdAtUtc: CreatedAtUtc,
            imagePath: imagePath);
        GenerationItemViewModel viewModel = new(
            item,
            0,
            imagePath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestLocalizationTextProvider.Default)
        {
            ThumbnailPath = thumbnailPath
        };

        return viewModel;
    }

    private static (Size Size, Vector Translation) Calculate(
        Size sourceSize,
        Rect previewBounds,
        Rect? viewportBounds = null)
    {
        return GenerationPreviewExpansionCalculator.Calculate(
            PreviewSize,
            sourceSize,
            previewBounds,
            viewportBounds ?? DefaultViewportBounds);
    }

    private static void AssertExpansion(
        Size sourceSize,
        Rect previewBounds,
        Size expectedSize,
        Vector expectedTranslation)
    {
        (Size size, Vector translation) = Calculate(sourceSize, previewBounds);

        size.Should().Be(expectedSize);
        translation.Should().Be(expectedTranslation);
    }

    private sealed class ExistingImagePaths : IDisposable
    {
        public GenerationItemViewModel Item { get; }
        public string ImagePath { get; }
        public string ThumbnailPath { get; }

        public ExistingImagePaths()
        {
            ImagePath = Path.GetTempFileName();
            ThumbnailPath = Path.GetTempFileName();
            Item = CreateItem(ImagePath, ThumbnailPath);
        }

        public void Dispose()
        {
            File.Delete(ImagePath);
            File.Delete(ThumbnailPath);
        }
    }
}
