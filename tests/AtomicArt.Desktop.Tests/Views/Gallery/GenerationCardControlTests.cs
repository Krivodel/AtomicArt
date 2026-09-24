using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Lang.Avalonia;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
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

    [Fact]
    public void ContextFlyout_WhenFavoriteChanges_UpdatesImbaActionLabel()
    {
        Dispatch(() =>
        {
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
                "image.webp",
                "thumbnail.jpg");
            item.IsFavorite = true;
            GenerationCardControl control = new()
            {
                DataContext = item
            };

            Show(control, GalleryLayoutService.CardWidth, 420d, window =>
            {
                Border container = control.FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException("Generation card container was not found.");
                AnimatedContextMenuFlyout flyout = container.ContextFlyout.Should()
                    .BeOfType<AnimatedContextMenuFlyout>().Subject;
                flyout.ShowAt(container);
                window.CaptureRenderedFrame();

                MenuItem menuItem = control.FindControl<MenuItem>("ImbaMenuItem")
                    ?? throw new InvalidOperationException("IMBA menu item was not found.");
                TextBlock header = menuItem.Header.Should()
                    .BeOfType<TextBlock>().Subject;
                header.Text.Should().Be(I18nManager.Instance.GetResource(
                    GalleryLocalizationKeys.Actions.RemoveImba));

                item.IsFavorite = false;
                window.CaptureRenderedFrame();

                header.Text.Should().Be(I18nManager.Instance.GetResource(
                    GalleryLocalizationKeys.Actions.Imba));

                item.IsFavorite = true;
                window.CaptureRenderedFrame();

                header.Text.Should().Be(I18nManager.Instance.GetResource(
                    GalleryLocalizationKeys.Actions.RemoveImba));
            });
        });
    }

    [Fact]
    public async Task ContextFlyout_WhenCreated_HasRequestedActionOrder()
    {
        await DispatchAsync(() =>
        {
            GenerationCardControl control = new();
            Border container = control.FindControl<Border>("GenerationCardContainer")
                ?? throw new InvalidOperationException("Generation card container was not found.");
            AnimatedContextMenuFlyout flyout = container.ContextFlyout.Should()
                .BeOfType<AnimatedContextMenuFlyout>().Subject;

            string?[] actionNames = flyout.Items
                .OfType<MenuItem>()
                .Select(menuItem => menuItem.Name)
                .ToArray();

            actionNames.Should().Equal(
                "CopyImageMenuItem",
                "SaveAsMenuItem",
                "ImbaMenuItem",
                "OpenDlss5MenuItem",
                "ShowInFolderMenuItem",
                "OpenWithMenuItem",
                "DeleteMenuItem",
                "SelectMenuItem");

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task OpenWithMenu_WhenOpened_UsesGalleryRevealAndListsPicaApplicationsBeforeChooser()
    {
        await DispatchAsync(() =>
        {
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
                "image.webp",
                "thumbnail.jpg");
            OpenWithApplication application = new(
                "image-editor",
                "Image Editor",
                GalleryThumbnailTestImages.CreatePngBytes(16, 16));
            GenerationCardControl control = new()
            {
                DataContext = item,
                LoadOpenWithApplicationsCommand = new AsyncRelayCommand<GenerationItemViewModel>(
                    loadedItem =>
                    {
                        ArgumentNullException.ThrowIfNull(loadedItem);
                        loadedItem.OpenWithApplications =
                            new List<OpenWithApplication> { application };
                        return Task.CompletedTask;
                    })
            };

            Show(control, GalleryLayoutService.CardWidth, 420d, window =>
            {
                Border container = control.FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException("Generation card container was not found.");
                AnimatedContextMenuFlyout flyout = container.ContextFlyout.Should()
                    .BeOfType<AnimatedContextMenuFlyout>().Subject;
                flyout.ShowAt(container);
                window.CaptureRenderedFrame();

                try
                {
                    MenuItem openWithMenuItem = control.FindControl<MenuItem>("OpenWithMenuItem")
                        ?? throw new InvalidOperationException("Open with menu item was not found.");
                    openWithMenuItem.Classes.Should().Contain("custom-submenu-animation");
                    openWithMenuItem.IsSubMenuOpen = true;
                    window.CaptureRenderedFrame();

                    Popup submenuPopup = openWithMenuItem
                        .GetVisualDescendants()
                        .OfType<Popup>()
                        .Single(popup => popup.Name == "PART_Popup");
                    submenuPopup.InheritsTransform.Should().BeTrue();
                    submenuPopup.Opacity.Should().Be(1d);
                    ContextMenuRevealHost revealHost = submenuPopup.Child
                        .Should()
                        .BeOfType<ContextMenuRevealHost>()
                        .Subject;
                    Border submenuBorder = revealHost
                        .GetVisualDescendants()
                        .OfType<Border>()
                        .Single(border => border.Name == "PART_Border");
                    submenuBorder.Background.Should().BeSameAs(
                        control.FindResource("ContextMenuBackgroundBrush"));
                    submenuBorder.BorderThickness.Should().Be(default(Thickness));
                    submenuBorder.Child.Should().BeOfType<Panel>()
                        .Subject.Background.Should().BeOfType<LinearGradientBrush>();
                    revealHost.GetVisualDescendants()
                        .OfType<LayoutTransformControl>()
                        .Should().BeEmpty();
                    Panel submenuChrome = revealHost.Child
                        .Should()
                        .BeOfType<Panel>()
                        .Subject;
                    submenuChrome.Children[0].IsVisible.Should().BeFalse();

                    MenuItem[] applications = openWithMenuItem.Items
                        .OfType<MenuItem>()
                        .ToArray();
                    applications.Should().HaveCount(2);
                    applications[0].CommandParameter.Should().Be(
                        new GalleryOpenWithApplicationRequest(item, "image.webp", application));
                    Image icon = applications[0].Icon.Should().BeOfType<Image>().Subject;
                    icon.Classes.Should().Contain("gallery-open-with-application-icon");
                    icon.Source.Should().NotBeNull();
                    double iconSize = control.FindResource("ContextMenuIconSize")
                        .Should()
                        .BeOfType<double>()
                        .Subject;
                    icon.Width.Should().Be(iconSize);
                    icon.Height.Should().Be(iconSize);
                    icon.Bounds.Width.Should().Be(iconSize);
                    icon.GetVisualAncestors().Should().Contain(applications[0]);
                    ContentPresenter submenuIconPresenter = icon.GetVisualAncestors()
                        .OfType<ContentPresenter>()
                        .Single(presenter => presenter.Name == "PART_IconPresenter");
                    Rectangle submenuSeparator = applications[0]
                        .GetVisualDescendants()
                        .OfType<Rectangle>()
                        .Single(rectangle => rectangle.Name == "PART_HorizontalSeparator");
                    ContentPresenter submenuHeaderPresenter = applications[0]
                        .GetVisualDescendants()
                        .OfType<ContentPresenter>()
                        .Single(presenter => presenter.Name == "PART_HeaderPresenter");
                    submenuIconPresenter.RenderTransform.Should().BeNull();
                    submenuSeparator.Width.Should().Be(0d);
                    submenuSeparator.Margin.Should().Be(default(Thickness));
                    submenuHeaderPresenter.Margin.Should().Be(new Thickness(8d, 0d, 5d, 0d));
                    GenerationCardControlTests.AssertIconHeaderGap(applications[0], icon);
                    applications[1].Name.Should().Be("ChooseApplicationMenuItem");

                    openWithMenuItem.IsSubMenuOpen = false;
                    submenuPopup.Child.Should().BeOfType<LayoutTransformControl>();
                    openWithMenuItem.Items.Should().ContainSingle()
                        .Which.Should().BeSameAs(applications[1]);

                    openWithMenuItem.IsSubMenuOpen = true;
                    window.CaptureRenderedFrame();
                    submenuPopup.Child.Should().BeOfType<ContextMenuRevealHost>();
                    openWithMenuItem.IsSubMenuOpen = false;
                }
                finally
                {
                    MenuItem? openWithMenuItem = control.FindControl<MenuItem>("OpenWithMenuItem");
                    if (openWithMenuItem is not null)
                    {
                        openWithMenuItem.IsSubMenuOpen = false;
                    }

                    flyout.Hide();
                }
            });

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task OpenWithMenu_WhileRevealing_DoesNotApplySecondOpacityAnimationAsync()
    {
        await DispatchAsync(async () =>
        {
            bool applicationChosen = false;
            GenerationCardControl control = new()
            {
                DataContext = GenerationCardControlTests.CreateItem(
                    "image.webp",
                    "thumbnail.jpg"),
                LoadOpenWithApplicationsCommand =
                    new AsyncRelayCommand<GenerationItemViewModel>(
                        _ => Task.CompletedTask),
                ChooseApplicationCommand = new RelayCommand(
                    () => applicationChosen = true)
            };
            Window window = Show(control, GalleryLayoutService.CardWidth, 420d);

            try
            {
                Border container = control.FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException(
                        "Generation card container was not found.");
                AnimatedContextMenuFlyout flyout = container.ContextFlyout.Should()
                    .BeOfType<AnimatedContextMenuFlyout>().Subject;
                flyout.ShowAt(container);
                window.CaptureRenderedFrame();

                MenuItem openWithMenuItem = control.FindControl<MenuItem>("OpenWithMenuItem")
                    ?? throw new InvalidOperationException(
                        "Open with menu item was not found.");
                openWithMenuItem.IsSubMenuOpen = true;
                window.CaptureRenderedFrame();

                Popup popup = openWithMenuItem
                    .GetVisualDescendants()
                    .OfType<Popup>()
                    .Single(candidate => candidate.Name == "PART_Popup");
                ContextMenuRevealHost revealHost = popup.Child.Should()
                    .BeOfType<ContextMenuRevealHost>().Subject;
                Panel content = revealHost.Child.Should()
                    .BeOfType<Panel>().Subject;

                await Task.Delay(75);

                content.Opacity.Should().Be(1d);
                revealHost.WidthRatio.Should().BeLessThan(1d);

                await Task.Delay(ContextMenuRevealHost.OpeningDurationMilliseconds);

                MenuItem chooseApplication = control
                    .FindControl<MenuItem>("ChooseApplicationMenuItem")
                    ?? throw new InvalidOperationException(
                        "Choose application menu item was not found.");
                GenerationCardControlTests.ClickMenuItem(
                    chooseApplication,
                    revealHost);
                applicationChosen.Should().BeTrue();
            }
            finally
            {
                MenuItem? openWithMenuItem = control.FindControl<MenuItem>("OpenWithMenuItem");
                if (openWithMenuItem is not null)
                {
                    openWithMenuItem.IsSubMenuOpen = false;
                }

                control.FindControl<Border>("GenerationCardContainer")
                    ?.ContextFlyout?.Hide();
                window.Close();
            }
        });
    }

    [Fact]
    public void OpenWithMenu_WhenPointerEnters_OpensWithoutHoverDelay()
    {
        Dispatch(() =>
        {
            GenerationCardControl control = new()
            {
                DataContext = GenerationCardControlTests.CreateItem(
                    "image.webp",
                    "thumbnail.jpg"),
                LoadOpenWithApplicationsCommand =
                    new AsyncRelayCommand<GenerationItemViewModel>(
                        _ => Task.CompletedTask)
            };

            Show(control, GalleryLayoutService.CardWidth, 420d, window =>
            {
                Border container = control.FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException(
                        "Generation card container was not found.");
                AnimatedContextMenuFlyout flyout = container.ContextFlyout.Should()
                    .BeOfType<AnimatedContextMenuFlyout>().Subject;
                flyout.ShowAt(container);
                window.CaptureRenderedFrame();

                try
                {
                    MenuItem openWithMenuItem = control.FindControl<MenuItem>("OpenWithMenuItem")
                        ?? throw new InvalidOperationException(
                            "Open with menu item was not found.");
                    Point pointerPosition = openWithMenuItem.TranslatePoint(
                            new Point(
                                openWithMenuItem.Bounds.Width / 2d,
                                openWithMenuItem.Bounds.Height / 2d),
                            window)
                        ?? throw new InvalidOperationException(
                            "Open with menu item position was not found.");

                    openWithMenuItem.IsSubMenuOpen.Should().BeFalse();

                    window.MouseMove(pointerPosition, RawInputModifiers.None);

                    openWithMenuItem.IsSubMenuOpen.Should().BeTrue();
                }
                finally
                {
                    flyout.Hide();
                }
            });
        });
    }

    [Fact]
    public void ContextFlyout_WhenOpened_RendersDlss5IconWithFillWithoutStroke()
    {
        Dispatch(() =>
        {
            GenerationCardControl control = new()
            {
                DataContext = GenerationCardControlTests.CreateItem("missing-image.png", "missing-thumbnail.jpg"),
                OpenDlss5Command = new RelayCommand(() => { })
            };

            Show(control, window =>
            {
                Border container = control.FindControl<Border>("GenerationCardContainer")
                    ?? throw new InvalidOperationException("Generation card container was not found.");
                AnimatedContextMenuFlyout flyout = container.ContextFlyout.Should()
                    .BeOfType<AnimatedContextMenuFlyout>().Subject;

                flyout.ShowAt(container);
                window.CaptureRenderedFrame();

                try
                {
                    MenuItem menuItem = control.FindControl<MenuItem>("OpenDlss5MenuItem")
                        ?? throw new InvalidOperationException("DLSS 5 menu item was not found.");
                    PathIcon icon = menuItem.Icon.Should().BeOfType<PathIcon>().Subject;
                    Avalonia.Controls.Shapes.Path shape = icon.GetVisualDescendants()
                        .OfType<Avalonia.Controls.Shapes.Path>().Single();

                    shape.Fill.Should().NotBeNull();
                    shape.Stroke.Should().BeNull();
                    shape.Data.Should().NotBeNull();
                    double iconSize = control.FindResource("ContextMenuIconSize").Should().BeOfType<double>().Subject;
                    icon.Bounds.Width.Should().Be(iconSize);
                }
                finally
                {
                    flyout.Hide();
                }
            });
        });
    }

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
        GenerationCardControlTests.AssertExpansion(
            new Size(440d, 220d),
            new Rect(780d, 40d, 220d, 220d),
            new Size(748d, 374d),
            new Vector(-528d, -40d));
    }

    [Fact]
    public void Calculate_WithTallSource_ScalesFullAspectRatioAndFitsViewportStart()
    {
        GenerationCardControlTests.AssertExpansion(
            new Size(220d, 440d),
            new Rect(40d, 380d, 220d, 220d),
            new Size(374d, 748d),
            new Vector(-40d, -380d));
    }

    [Fact]
    public void Calculate_WithCenteredWideSource_ExpandsEvenlyAroundPreview()
    {
        GenerationCardControlTests.AssertExpansion(
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

        (Size size, Vector translation) = GenerationCardControlTests.Calculate(
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
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(imagePath, thumbnailPath);

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
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(imagePath, thumbnailPath);

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
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
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
    public void PreviewArea_WhenClicked_ExecutesOpenViewerCommand()
    {
        Dispatch(() =>
        {
            using ExistingImagePaths paths = new();
            bool opened = false;
            RelayCommand command = new(() => opened = true);
            GenerationCardControl control = new()
            {
                DataContext = paths.Item,
                OpenViewerCommand = command
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                window.MouseDown(new Point(110d, 110d), MouseButton.Left);
                window.MouseUp(new Point(110d, 110d), MouseButton.Left);

                opened.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ContextFlyout_WhenCardCreated_ContainsExpectedItemsAndIcons()
    {
        await DispatchAsync(() =>
        {
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            RelayCommand selectCommand = new(() => item.IsSelected = true);
            RelayCommand copyCommand = new(() => { });
            RelayCommand revealCommand = new(() => { });
            RelayCommand deleteCommand = new(() => { });
            RelayCommand favoriteCommand = new(() => { });
            GenerationCardControl control = new()
            {
                DataContext = item,
                CopyImageCommand = copyCommand,
                DeleteOrCancelCommand = deleteCommand,
                RevealInFolderCommand = revealCommand,
                ToggleFavoriteCommand = favoriteCommand,
                ToggleSelectionCommand = selectCommand
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                Button revealInFolderButton = control
                    .FindControl<Button>("RevealInFolderButton")
                    ?? throw new InvalidOperationException(
                        "Reveal-in-folder button was not found.");
                revealInFolderButton.Width.Should().Be(36d);
                revealInFolderButton.Height.Should().Be(36d);
                revealInFolderButton.Margin.Should().Be(new Thickness(6d));
                Avalonia.Controls.Shapes.Path cardFolderIcon = revealInFolderButton
                    .GetVisualDescendants()
                    .OfType<Avalonia.Controls.Shapes.Path>()
                    .Single(path => path.Classes.Contains("gallery-outline-icon"));
                cardFolderIcon.Data?.ToString().Should().Be(
                    StreamGeometry.Parse(ViewerActionIconGeometry.ShowInFolder).ToString());
                cardFolderIcon.Fill.Should().BeNull();
                cardFolderIcon.Stretch.Should().Be(Stretch.Uniform);
                cardFolderIcon.Width.Should().Be(20d);
                cardFolderIcon.Height.Should().Be(20d);
                cardFolderIcon.Stroke.Should().NotBeNull();
                cardFolderIcon.StrokeThickness.Should().Be(1.5d);
                DropShadowEffect cardFolderShadow = cardFolderIcon.Effect
                    .Should()
                    .BeOfType<DropShadowEffect>()
                    .Subject;
                cardFolderShadow.BlurRadius.Should().Be(3d);
                cardFolderShadow.OffsetY.Should().Be(1d);
                cardFolderShadow.Opacity.Should().Be(0.8d);

                Button deleteButton = control
                    .FindControl<Button>("DeleteButton")
                    ?? throw new InvalidOperationException(
                        "Delete button was not found.");
                deleteButton.Width.Should().Be(36d);
                deleteButton.Height.Should().Be(36d);
                deleteButton.Margin.Should().Be(new Thickness(6d));
                Avalonia.Controls.Shapes.Path cardDeleteIcon = deleteButton
                    .GetVisualDescendants()
                    .OfType<Avalonia.Controls.Shapes.Path>()
                    .Single(path => path.IsVisible);
                cardDeleteIcon.Data.Should().BeSameAs(
                    control.FindResource("GalleryContextMenuDeleteIcon"));
                cardDeleteIcon.Classes.Should().Contain("Danger");
                cardDeleteIcon.Fill.Should().BeNull();
                cardDeleteIcon.Stretch.Should().Be(Stretch.Uniform);
                cardDeleteIcon.Width.Should().Be(17d);
                cardDeleteIcon.Height.Should().Be(20d);
                cardDeleteIcon.Stroke.Should().NotBeNull();
                cardDeleteIcon.StrokeThickness.Should().Be(1.5d);
                ISolidColorBrush deleteBrush = cardDeleteIcon.Stroke
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Subject;
                deleteBrush.Color.Should().Be(control.FindResource("GalleryDeleteIconColor"));
                DropShadowEffect cardDeleteShadow = cardDeleteIcon.Effect
                    .Should()
                    .BeOfType<DropShadowEffect>()
                    .Subject;
                cardDeleteShadow.BlurRadius.Should().Be(cardFolderShadow.BlurRadius);
                cardDeleteShadow.OffsetY.Should().Be(cardFolderShadow.OffsetY);
                cardDeleteShadow.Opacity.Should().Be(cardFolderShadow.Opacity);

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
                MenuItem copyMenuItem = control
                    .FindControl<MenuItem>("CopyImageMenuItem")
                    ?? throw new InvalidOperationException(
                        "Copy-image menu item was not found.");
                MenuItem saveAsMenuItem = control
                    .FindControl<MenuItem>("SaveAsMenuItem")
                    ?? throw new InvalidOperationException(
                        "Save-as menu item was not found.");
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

                Avalonia.Controls.Shapes.Path copyIcon = copyMenuItem.Icon
                    .Should()
                    .BeOfType<Avalonia.Controls.Shapes.Path>()
                    .Subject;
                copyIcon.Data.Should().BeOfType<StreamGeometry>();
                copyMenuItem.Command.Should().BeSameAs(copyCommand);
                copyMenuItem.CommandParameter.Should().BeSameAs(item);
                Avalonia.Controls.Shapes.Path saveAsIcon = saveAsMenuItem.Icon
                    .Should()
                    .BeOfType<Avalonia.Controls.Shapes.Path>()
                    .Subject;
                saveAsIcon.Data.Should().BeOfType<StreamGeometry>();
                Avalonia.Controls.Shapes.Path selectIcon = selectMenuItem.Icon
                    .Should()
                    .BeOfType<Avalonia.Controls.Shapes.Path>()
                    .Subject;
                selectIcon.Fill.Should().BeNull();
                control.TryFindResource(
                    "GalleryContextMenuSelectionIcon",
                    out object? selectionResource).Should().BeTrue();
                selectIcon.Data.Should().BeSameAs(selectionResource);
                selectIcon.Effect.Should().BeNull();
                selectMenuItem.Command.Should().BeSameAs(selectCommand);
                selectMenuItem.CommandParameter.Should().BeSameAs(item);
                selectMenuItem.IsEnabled.Should().BeTrue();
                Avalonia.Controls.Shapes.Path folderIcon = showInFolderMenuItem.Icon
                    .Should()
                    .BeOfType<Avalonia.Controls.Shapes.Path>()
                    .Subject;
                folderIcon.Data?.ToString().Should().Be(
                    StreamGeometry.Parse(ViewerActionIconGeometry.ShowInFolder).ToString());
                showInFolderMenuItem.Command.Should().BeSameAs(revealCommand);
                showInFolderMenuItem.CommandParameter.Should().BeSameAs(item);
                Avalonia.Controls.Shapes.Path imbaIcon = imbaMenuItem.Icon
                    .Should()
                    .BeOfType<Avalonia.Controls.Shapes.Path>()
                    .Subject;
                imbaIcon.Data?.ToString().Should().Be(
                    StreamGeometry.Parse(ViewerActionIconGeometry.Star).ToString());
                imbaMenuItem.IsEnabled.Should().BeTrue();
                imbaMenuItem.Command.Should().BeSameAs(favoriteCommand);
                imbaMenuItem.CommandParameter.Should().BeSameAs(item);
                Grid deleteIconHost = deleteMenuItem.Icon
                    .Should()
                    .BeOfType<Grid>()
                    .Subject;
                deleteIconHost.Width.Should().Be(18d);
                deleteIconHost.Height.Should().Be(18d);
                Avalonia.Controls.Shapes.Path deleteIcon = deleteIconHost.Children
                    .OfType<Avalonia.Controls.Shapes.Path>()
                    .Single();
                deleteIcon.HorizontalAlignment.Should().Be(HorizontalAlignment.Center);
                deleteIcon.VerticalAlignment.Should().Be(VerticalAlignment.Center);
                control.TryFindResource(
                    "GalleryContextMenuDeleteIcon",
                    out object? deleteResource).Should().BeTrue();
                deleteIcon.Data.Should().BeSameAs(deleteResource);
                deleteMenuItem.Command.Should().BeSameAs(deleteCommand);
                deleteMenuItem.CommandParameter.Should().BeSameAs(item);
                menuFlyout.Popup.WindowManagerAddShadowHint.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void CardActions_WhenPreviewIsExpanded_AreDisabledAndNotHitTestVisible()
    {
        Dispatch(() =>
        {
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
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
                Button revealInFolderButton = control
                    .FindControl<Button>("RevealInFolderButton")
                    ?? throw new InvalidOperationException(
                        "Reveal-in-folder button was not found.");
                Button deleteButton = control
                    .FindControl<Button>("DeleteButton")
                    ?? throw new InvalidOperationException(
                        "Delete button was not found.");

                revealInFolderButton.IsEnabled.Should().BeTrue();
                revealInFolderButton.IsHitTestVisible.Should().BeTrue();
                deleteButton.IsEnabled.Should().BeTrue();
                deleteButton.IsHitTestVisible.Should().BeTrue();

                control.Classes.Add(
                    GenerationPreviewExpansionVisualMetrics.ExpandedClass);
                window.CaptureRenderedFrame();

                revealInFolderButton.IsEnabled.Should().BeFalse();
                revealInFolderButton.IsHitTestVisible.Should().BeFalse();
                deleteButton.IsEnabled.Should().BeFalse();
                deleteButton.IsHitTestVisible.Should().BeFalse();

                control.Classes.Remove(
                    GenerationPreviewExpansionVisualMetrics.ExpandedClass);
                window.CaptureRenderedFrame();

                revealInFolderButton.IsEnabled.Should().BeTrue();
                revealInFolderButton.IsHitTestVisible.Should().BeTrue();
                deleteButton.IsEnabled.Should().BeTrue();
                deleteButton.IsHitTestVisible.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ContextFlyout_WhenCardRightPressed_OpensBeforeReleaseWithoutReopening()
    {
        Dispatch(() =>
        {
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
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

                menuFlyout.IsOpen.Should().BeTrue();
                ContextMenuRevealHost revealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;

                window.MouseUp(cardCenter, MouseButton.Right);

                menuFlyout.IsOpen.Should().BeTrue();
                menuFlyout.Popup.Child.Should().BeSameAs(revealHost);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ContextFlyout_WhenCardRightClicked_OpensWithLivePresenterRevealAsync()
    {
        await DispatchAsync(async () =>
        {
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            GenerationCardControl control = new()
            {
                DataContext = item
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                Math.Max(GalleryLayoutService.CardHeight, 420d));

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
                bool backgroundFound = presenter.TryFindResource(
                    "ContextMenuBackgroundBrush",
                    out object? backgroundResource);
                LinearGradientBrush backgroundBrush = presenter.Background
                    .Should()
                    .BeOfType<LinearGradientBrush>()
                    .Subject;
                menuFlyout.IsOpen.Should().BeTrue();
                menuItems.Should().HaveCount(8);
                menuItems[7].Should().BeSameAs(selectMenuItem);
                selectMenuItem.IsSelected.Should().BeFalse();
                selectMenuItem.IsPointerOver.Should().BeFalse();
                selectMenuItem.IsFocused.Should().BeFalse();
                presenter.Focusable.Should().BeTrue();
                (presenter.IsFocused || menuItems.Any(menuItem => menuItem.IsFocused))
                    .Should().BeTrue();
                presenterTemplateRoot.Margin.Should().Be(default(Thickness));
                presenterChromeBorders.Should().HaveCount(2);
                presenterChromeBorders.Should().OnlyContain(
                    border => border.Margin == default);
                presenterChromeBorders[0].IsVisible.Should().BeFalse();
                presenterChromeBorders[1].IsVisible.Should().BeTrue();
                iconSeparators.Should().HaveCount(8);
                iconSeparators.Should().OnlyContain(separator =>
                    separator.Opacity == 0d
                    && separator.Width == 0d
                    && separator.Margin == default);
                iconPresenters.Should().HaveCount(8);
                headerPresenters.Should().HaveCount(8);
                menuHeaderTextBlocks.Should().HaveCount(8);
                menuHeaderTextBlocks.Should().OnlyContain(
                    textBlock => textBlock.FontWeight == FontWeight.Normal);

                Avalonia.Controls.Shapes.Path[] pathIcons = iconPresenters
                    .SelectMany(iconPresenter => iconPresenter
                        .GetVisualDescendants()
                        .OfType<Avalonia.Controls.Shapes.Path>())
                    .Where(path => path.Classes.Contains("gallery-outline-icon"))
                    .ToArray();
                pathIcons.Should().HaveCount(7);
                double contextIconSize = control.FindResource("ContextMenuIconSize")
                    .Should().BeOfType<double>().Subject;
                contextIconSize.Should().Be(18d);

                pathIcons.Should().OnlyContain(path =>
                    (object.ReferenceEquals(path.Fill, null))
                    && (!object.ReferenceEquals(path.Stroke, null))
                    && (path.Height == contextIconSize)
                    && (path.StrokeThickness == 1.5d)
                    && (path.StrokeLineCap == PenLineCap.Round)
                    && (path.StrokeJoin == PenLineJoin.Round));
                pathIcons.Where(path => !path.Classes.Contains("Danger"))
                    .Should().OnlyContain(path => path.Width == contextIconSize);

                Avalonia.Controls.Shapes.Path dangerPath = pathIcons
                    .Single(path => path.Classes.Contains("Danger"));
                dangerPath.Stroke.Should().NotBeNull();
                dangerPath.Width.Should().Be(15.3d);

                iconPresenters.Should().OnlyContain(iconPresenter =>
                    iconPresenter.RenderTransform == null);

                foreach (ContentPresenter headerPresenter in headerPresenters)
                {
                    headerPresenter.RenderTransform.Should().BeNull();
                    headerPresenter.Margin.Should().Be(new Thickness(8d, 0d, 5d, 0d));
                }

                GenerationCardControlTests.AssertIconHeaderGap(
                    menuItems[0],
                    (Control)menuItems[0].Icon!);

                presenter.RenderTransform.Should().BeNull();
                presenter.Opacity.Should().Be(1d);
                presenter.Clip.Should().BeOfType<RectangleGeometry>();
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
                revealHost.BoxShadows.Should().NotBe(default(BoxShadows));
                revealHost.Padding.Left.Should().BeGreaterThan(0d);
                revealHost.Padding.Top.Should().BeGreaterThan(0d);
                revealHost.Padding.Right.Should().BeGreaterThan(0d);
                revealHost.Padding.Bottom.Should().BeGreaterThan(0d);

                await Task.Delay(
                    ContextMenuRevealHost.OpeningDurationMilliseconds + 50);

                iconSeparators.Should().OnlyContain(separator => separator.Opacity == 0d);
                presenter.Clip.Should().BeNull();

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
    public async Task ContextFlyout_WhenCardIsScaled_MenuAndSubmenuInheritUiScaleAsync(
        double uiScale)
    {
        await DispatchAsync(() =>
        {
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            GenerationCardControl control = new()
            {
                DataContext = item,
                LoadOpenWithApplicationsCommand =
                    new AsyncRelayCommand<GenerationItemViewModel>(
                        _ => Task.CompletedTask)
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
                GenerationCardControlTests.StartContextMenuOpening(window);

                ContextMenuRevealHost revealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;
                PopupAssertions.AssertInheritsScale(
                    menuFlyout.Popup,
                    revealHost,
                    uiScale);

                MenuItem openWithMenuItem = control.FindControl<MenuItem>("OpenWithMenuItem")
                    ?? throw new InvalidOperationException(
                        "Open with menu item was not found.");
                openWithMenuItem.IsSubMenuOpen = true;
                window.CaptureRenderedFrame();

                Popup submenuPopup = openWithMenuItem
                    .GetVisualDescendants()
                    .OfType<Popup>()
                    .Single(popup => popup.Name == "PART_Popup");
                ContextMenuRevealHost submenuRevealHost = submenuPopup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;
                PopupAssertions.AssertInheritsScale(
                    submenuPopup,
                    submenuRevealHost,
                    uiScale);

                openWithMenuItem.IsSubMenuOpen = false;
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
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
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
                GenerationCardControlTests.StartContextMenuOpening(window);
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
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
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
                GenerationCardControlTests.StartContextMenuOpening(window);
                ContextMenuRevealHost revealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;
                MenuItem showInFolderMenuItem = control
                    .FindControl<MenuItem>("ShowInFolderMenuItem")
                    ?? throw new InvalidOperationException(
                        "Show-in-folder menu item was not found.");
                revealHost.WidthRatio.Should().BeLessThan(1d);
                MenuFlyoutPresenter presenter = revealHost.Child
                    .Should()
                    .BeOfType<MenuFlyoutPresenter>()
                    .Subject;
                presenter.IsHitTestVisible.Should().BeTrue();

                GenerationCardControlTests.ClickMenuItem(showInFolderMenuItem, revealHost);

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
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
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
                revealHost.BoxShadows.Should().NotBe(default(BoxShadows));

                menuFlyout.Hide();

                menuFlyout.IsOpen.Should().BeTrue();
                Task completedTask = await Task.WhenAny(
                    closed.Task,
                    Task.Delay(GenerationCardControlTests.AnimationCompletionTimeout));
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
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
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
                GenerationCardControlTests.StartContextMenuOpening(window);
                ContextMenuRevealHost firstRevealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;
                MenuItem showInFolderMenuItem = control
                    .FindControl<MenuItem>("ShowInFolderMenuItem")
                    ?? throw new InvalidOperationException(
                        "Show-in-folder menu item was not found.");
                firstRevealHost.WidthRatio.Should().BeLessThan(1d);
                TaskCompletionSource<bool> firstClose = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                menuFlyout.Closed += OnFirstClosed;

                GenerationCardControlTests.ClickMenuItem(showInFolderMenuItem, firstRevealHost);

                Task firstCompletedTask = await Task.WhenAny(
                    firstClose.Task,
                    Task.Delay(GenerationCardControlTests.AnimationCompletionTimeout));
                firstCompletedTask.Should().BeSameAs(firstClose.Task);
                menuFlyout.Closed -= OnFirstClosed;
                window.MouseDown(cardCenter, MouseButton.Right);
                window.MouseUp(cardCenter, MouseButton.Right);
                GenerationCardControlTests.StartContextMenuOpening(window);
                ContextMenuRevealHost secondRevealHost = menuFlyout.Popup.Child
                    .Should()
                    .BeOfType<ContextMenuRevealHost>()
                    .Subject;

                menuFlyout.IsOpen.Should().BeTrue();
                secondRevealHost.WidthRatio.Should().BeLessThan(1d);
                await Task.Delay(
                    ContextMenuRevealHost.OpeningDurationMilliseconds + 50);

                MenuFlyoutPresenter secondPresenter = secondRevealHost.Child
                    .Should()
                    .BeOfType<MenuFlyoutPresenter>()
                    .Subject;
                secondRevealHost.Opacity.Should().Be(1d);
                secondPresenter.Opacity.Should().Be(1d);
                secondPresenter.IsHitTestVisible.Should().BeTrue();
                TaskCompletionSource<bool> secondClose = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                menuFlyout.Closed += OnSecondClosed;

                menuFlyout.Hide();

                Task secondCompletedTask = await Task.WhenAny(
                    secondClose.Task,
                    Task.Delay(GenerationCardControlTests.AnimationCompletionTimeout));
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
        "Сделай первую картинку (игра 3 в ряд, 2д) в стиле второй "
        + "картинки (игра borderlands 3). Не меняй игру на 1 картинке, "
        + "нужны лишь другие текстуры/рисовка/фон.")]
    [InlineData(
        "Первая строка\n"
        + "Вторая строка\n"
        + "Третья строка\n"
        + "Скрытая четвёртая строка")]
    [InlineData(
        "ОченьДлинныйНеразрывныйПромптОченьДлинныйНеразрывныйПромпт"
        + "ОченьДлинныйНеразрывныйПромптОченьДлинныйНеразрывныйПромпт"
        + "ОченьДлинныйНеразрывныйПромптОченьДлинныйНеразрывныйПромпт")]
    public async Task Prompt_WhenReusedCardTextOverflows_EndsWithEllipsisAsync(
        string overflowingPrompt)
    {
        await DispatchAsync(() =>
        {
            const string Ellipsis = "…";
            GenerationItemViewModel initialItem = GenerationCardControlTests.CreateItem(
                "initial-image.png",
                "initial-thumbnail.jpg");
            GenerationItemViewModel overflowingItem = GenerationCardControlTests.CreateItem(
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
                TextBlock initialPrompt = GenerationCardControlTests.GetPromptTextBlock(control, Prompt);
                GenerationCardControlTests.GetRenderedText(initialPrompt).Should().Be(Prompt);

                control.DataContext = overflowingItem;
                Size cardSize = new(
                    GalleryLayoutService.CardWidth,
                    GalleryLayoutService.CardHeight);
                control.Measure(cardSize);
                control.Arrange(new Rect(cardSize));

                OverflowEllipsisTextBlock prompt = GenerationCardControlTests.GetPromptTextBlock(
                        control,
                        overflowingPrompt)
                    .Should()
                    .BeOfType<OverflowEllipsisTextBlock>()
                    .Subject;
                prompt.TextTrimming.Should().NotBe(TextTrimming.None);
                prompt.TextLayout.TextLines.Should().HaveCount(prompt.MaxLines);
                string renderedText = GenerationCardControlTests.GetRenderedText(prompt);
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
            GenerationItemViewModel item = GenerationCardControlTests.CreateItem(
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
                TextBlock prompt = GenerationCardControlTests.GetPromptTextBlock(control, Prompt);
                TextBlock model = GenerationCardControlTests.GetPromptTextBlock(
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

    private static void AssertIconHeaderGap(MenuItem menuItem, Control icon)
    {
        ContentPresenter headerPresenter = menuItem
            .GetVisualDescendants()
            .OfType<ContentPresenter>()
            .Single(presenter => presenter.Name == "PART_HeaderPresenter");
        Point iconRight = icon.TranslatePoint(
                new Point(icon.Bounds.Width, 0d),
                menuItem)
            ?? throw new InvalidOperationException("Menu icon position was not found.");
        Point headerLeft = headerPresenter.TranslatePoint(default(Point), menuItem)
            ?? throw new InvalidOperationException("Menu header position was not found.");

        (headerLeft.X - iconRight.X).Should().BeApproximately(8d, 0.5d);
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
            id: GenerationCardControlTests.ItemId,
            prompt: prompt,
            aspectRatio: AspectRatio,
            createdAtUtc: GenerationCardControlTests.CreatedAtUtc,
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
            GenerationCardControlTests.PreviewSize,
            sourceSize,
            previewBounds,
            viewportBounds ?? GenerationCardControlTests.DefaultViewportBounds);
    }

    private static void AssertExpansion(
        Size sourceSize,
        Rect previewBounds,
        Size expectedSize,
        Vector expectedTranslation)
    {
        (Size size, Vector translation) = GenerationCardControlTests.Calculate(sourceSize, previewBounds);

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
            Item = GenerationCardControlTests.CreateItem(ImagePath, ThumbnailPath);
        }

        public void Dispose()
        {
            File.Delete(ImagePath);
            File.Delete(ThumbnailPath);
        }
    }
}
