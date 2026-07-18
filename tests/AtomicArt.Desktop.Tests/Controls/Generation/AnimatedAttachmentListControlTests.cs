using System.Collections.ObjectModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services.GalleryAnimation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Tests.Controls.Generation;

public sealed class AnimatedAttachmentListControlTests : AnimatedGalleryControlTestBase
{
    private const double AttachmentSlotWidth = 64d;

    [Theory]
    [InlineData(0d, 0)]
    [InlineData(63d, 0)]
    [InlineData(64d, 1)]
    [InlineData(160d, 2)]
    [InlineData(400d, 2)]
    public void CalculateTargetIndex_WithDraggedCenterX_ClampsToAvailableAttachmentRange(
        double draggedCenterX,
        int expectedIndex)
    {
        int targetIndex = AnimatedAttachmentListControl.CalculateTargetIndex(
            draggedCenterX,
            3,
            AttachmentSlotWidth);

        targetIndex.Should().Be(expectedIndex);
    }

    [Fact]
    public void CreateRemoveFrames_WithDifferentItemIds_CanChooseDifferentHorizontalDirections()
    {
        Guid rightId = CreateGuid(firstByte: 0);
        Guid leftId = CreateGuid(firstByte: 128);

        MotionFrame rightFrame = AnimatedAttachmentListControl.CreateRemoveFrames(rightId).Last();
        MotionFrame leftFrame = AnimatedAttachmentListControl.CreateRemoveFrames(leftId).Last();

        rightFrame.X.Should().BePositive();
        leftFrame.X.Should().BeNegative();
    }

    [Fact]
    public void CreateSpawnFrames_WithDifferentItemIds_CanChooseDifferentHorizontalDirections()
    {
        Guid rightId = CreateGuid(firstByte: 0);
        Guid leftId = CreateGuid(firstByte: 128);

        MotionFrame rightFrame = AnimatedAttachmentListControl.CreateSpawnFrames(rightId).First();
        MotionFrame leftFrame = AnimatedAttachmentListControl.CreateSpawnFrames(leftId).First();

        rightFrame.X.Should().BeNegative();
        leftFrame.X.Should().BePositive();
    }

    [Fact]
    public void Layout_WhenSingleAttachmentHasWideViewport_KeepsAttachmentPanelLeftAligned()
    {
        Dispatch(() =>
        {
            ObservableCollection<AttachedImageViewModel> items = [CreateItem("first.png")];
            AnimatedAttachmentListControl control = new()
            {
                Items = items
            };
            Window window = Show(control, 420d, 96d);

            try
            {
                Canvas panel = GetAttachmentPanel(control);

                Point panelPosition = panel.TranslatePoint(new Point(0d, 0d), control)
                    ?? throw new InvalidOperationException("Attachment panel position was not available.");

                panelPosition.X.Should().Be(0d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Layout_WithLoadingAndReadyAttachments_HasNoPreviewBorder()
    {
        Dispatch(() =>
        {
            ObservableCollection<AttachedImageViewModel> items =
            [
                AttachedImageViewModel.CreateLoading("loading.png"),
                CreateItem("ready.png")
            ];
            AnimatedAttachmentListControl control = new()
            {
                Items = items
            };
            Window window = Show(control, 160d, 96d);

            try
            {
                List<Border> previews = control
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Where(border => border.Classes.Contains("attachment-preview"))
                    .ToList();

                previews.Should().HaveCount(2);
                previews.Should().OnlyContain(
                    preview => preview.BorderThickness == new Thickness(0d));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Image_WhenEntryIsCreated_UsesMediumBitmapInterpolation()
    {
        Dispatch(() =>
        {
            ObservableCollection<AttachedImageViewModel> items = [CreateItem("ready.png")];
            AnimatedAttachmentListControl control = new()
            {
                Items = items
            };
            Window window = Show(control, 160d, 96d);

            try
            {
                Image image = control
                    .GetVisualDescendants()
                    .OfType<Image>()
                    .Single();

                RenderOptions.GetBitmapInterpolationMode(image)
                    .Should()
                    .Be(BitmapInterpolationMode.MediumQuality);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CollectionChanged_WhenInsertedAtFront_ShiftsExistingAttachmentRightAndAnimatesNewAttachment()
    {
        Dispatch(() =>
        {
            AttachedImageViewModel firstItem = CreateItem("first.png");
            ObservableCollection<AttachedImageViewModel> items = [firstItem];
            AnimatedAttachmentListControl control = new()
            {
                Items = items
            };
            Window window = Show(control, 240d, 96d);

            try
            {
                Canvas panel = GetAttachmentPanel(control);
                Control firstControl = panel.Children.OfType<Control>().Single();

                items.Insert(0, CreateItem("second.png"));
                window.CaptureRenderedFrame();

                panel.Children.OfType<Control>().Should().HaveCount(2);
                Canvas.GetLeft(firstControl).Should().Be(AttachmentSlotWidth);
                GetTranslateTransform(firstControl).X.Should().Be(-AttachmentSlotWidth);

                Control insertedControl = panel.Children
                    .OfType<Control>()
                    .Single(child => !ReferenceEquals(child, firstControl));
                Canvas.GetLeft(insertedControl).Should().Be(0d);
                insertedControl.Opacity.Should().Be(0d);
                TransformGroup transformGroup = GetTransformGroup(insertedControl);
                TranslateTransform translate = transformGroup.Children
                    .OfType<TranslateTransform>()
                    .Single();
                ScaleTransform scale = transformGroup.Children
                    .OfType<ScaleTransform>()
                    .Single();

                (Math.Abs(translate.X) + Math.Abs(translate.Y)).Should().BeGreaterThan(0d);
                scale.ScaleX.Should().Be(0.94d);
                scale.ScaleY.Should().Be(0.94d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CollectionChanged_WhenAppendedPastViewport_ScrollsToEnd()
    {
        Dispatch(() =>
        {
            ObservableCollection<AttachedImageViewModel> items =
            [
                CreateItem("first.png"),
                CreateItem("second.png"),
                CreateItem("third.png")
            ];
            AnimatedAttachmentListControl control = new()
            {
                Items = items
            };
            Window window = Show(control, 160d, 96d);

            try
            {
                ScrollViewer scrollViewer = GetAttachmentScrollViewer(control);
                Canvas panel = GetAttachmentPanel(control);

                items.Add(CreateItem("fourth.png"));

                for (int i = 0; i < 30; i++)
                {
                    window.CaptureRenderedFrame();
                }

                double expectedOffsetX = Math.Max(0d, panel.Width - scrollViewer.Viewport.Width);
                scrollViewer.Offset.X.Should().BeApproximately(expectedOffsetX, 1d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CollectionChanged_WhenRemovedTwiceBeforeAnimationCompletes_LeavesEachRemovedAttachmentInOverlay()
    {
        Dispatch(() =>
        {
            ObservableCollection<AttachedImageViewModel> items =
            [
                CreateItem("first.png"),
                CreateItem("second.png"),
                CreateItem("third.png")
            ];
            AnimatedAttachmentListControl control = new()
            {
                Items = items
            };
            Window window = Show(control, 260d, 96d);

            try
            {
                Canvas panel = GetAttachmentPanel(control);
                Canvas overlay = GetOverlayCanvas(control);

                items.RemoveAt(0);
                window.CaptureRenderedFrame();

                panel.Children.OfType<Control>().Should().HaveCount(2);
                overlay.Children.OfType<Control>().Should().ContainSingle();

                items.RemoveAt(0);
                window.CaptureRenderedFrame();

                panel.Children.OfType<Control>().Should().ContainSingle();
                overlay.Children.OfType<Control>().Should().HaveCount(2);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CollectionChanged_WhenLoadingAttachmentIsRemoved_UsesAnimatedRemovalPath()
    {
        Dispatch(() =>
        {
            ObservableCollection<AttachedImageViewModel> items =
            [
                AttachedImageViewModel.CreateLoading("loading.png")
            ];
            AnimatedAttachmentListControl control = new()
            {
                Items = items
            };
            Window window = Show(control, 160d, 96d);

            try
            {
                Canvas panel = GetAttachmentPanel(control);
                Canvas overlay = GetOverlayCanvas(control);

                items.Clear();
                window.CaptureRenderedFrame();

                panel.Children.OfType<Control>().Should().BeEmpty();
                overlay.Children.OfType<Control>().Should().ContainSingle();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ItemState_WhenPreparationCompletes_FadesImageInWhilePixelsDisappear()
    {
        await DispatchAsync(async () =>
        {
            AttachedImageViewModel pendingItem = AttachedImageViewModel.CreateLoading("pending.png");
            ObservableCollection<AttachedImageViewModel> items = [pendingItem];
            AnimatedAttachmentListControl control = new()
            {
                Items = items
            };
            Window window = Show(control, 160d, 96d);

            try
            {
                Image image = control
                    .GetVisualDescendants()
                    .OfType<Image>()
                    .Single();
                AttachmentPixelLoadingControl loadingIndicator = control
                    .GetVisualDescendants()
                    .OfType<AttachmentPixelLoadingControl>()
                    .Single();

                image.IsVisible.Should().BeFalse();
                loadingIndicator.IsVisible.Should().BeTrue();

                AttachedImageDto dto = CreateDto("pending.png");
                pendingItem.Complete(dto, CreateState(dto));

                for (int attempt = 0; attempt < 100 && image.Source is null; attempt++)
                {
                    await Task.Delay(10);
                }

                window.CaptureRenderedFrame();

                image.Source.Should().NotBeNull();
                image.IsVisible.Should().BeTrue();
                image.Opacity.Should().BeLessThan(1d);
                loadingIndicator.IsVisible.Should().BeTrue();

                for (int attempt = 0; attempt < 100 && image.Opacity < 1d; attempt++)
                {
                    await Task.Delay(10);
                    window.CaptureRenderedFrame();
                }

                image.IsVisible.Should().BeTrue();
                image.Opacity.Should().Be(1d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Canvas GetAttachmentPanel(AnimatedAttachmentListControl control)
    {
        ScrollViewer scrollViewer = GetAttachmentScrollViewer(control);

        if (scrollViewer.Content is not Canvas panel)
        {
            throw new InvalidOperationException("Attachment panel was not found.");
        }

        return panel;
    }

    private static ScrollViewer GetAttachmentScrollViewer(AnimatedAttachmentListControl control)
    {
        return GetRootGrid(control)
            .Children
            .OfType<ScrollViewer>()
            .Single();
    }

    private static Canvas GetOverlayCanvas(AnimatedAttachmentListControl control)
    {
        return GetRootGrid(control)
            .Children
            .OfType<Canvas>()
            .Single();
    }

    private static Grid GetRootGrid(AnimatedAttachmentListControl control)
    {
        if (control.Content is not Grid root)
        {
            throw new InvalidOperationException("Attachment list root grid was not found.");
        }

        return root;
    }

    private static TransformGroup GetTransformGroup(Control control)
    {
        if (control.RenderTransform is not TransformGroup transformGroup)
        {
            throw new InvalidOperationException("Attachment transform was not found.");
        }

        return transformGroup;
    }

    private static AttachedImageViewModel CreateItem(string fileName)
    {
        AttachedImageDto dto = CreateDto(fileName);
        PanelAttachmentState state = CreateState(dto);

        return new AttachedImageViewModel(dto, state);
    }

    private static AttachedImageDto CreateDto(string fileName)
    {
        return new AttachedImageDto(
            fileName,
            GenerationImageContentTypes.Png,
            GenerationImageTestData.ValidPngBytes);
    }

    private static PanelAttachmentState CreateState(AttachedImageDto dto)
    {
        return new PanelAttachmentState
        {
            Id = dto.FileName,
            FileName = dto.FileName,
            ContentType = dto.ContentType,
            SizeBytes = dto.Content.LongLength,
            InternalFileName = dto.FileName
        };
    }

    private static Guid CreateGuid(byte firstByte)
    {
        byte[] bytes = new byte[16];
        bytes[0] = firstByte;

        return new Guid(bytes);
    }
}
