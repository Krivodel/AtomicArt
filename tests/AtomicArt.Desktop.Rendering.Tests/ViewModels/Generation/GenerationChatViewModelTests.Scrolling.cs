using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChatView_WithHistoryFade_FillsTheSurfaceAndMasksOnlyTheClippedBottomEdge(bool overflowing)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationChatMessage[] messages = Enumerable.Range(0, overflowing ? 40 : 1)
                .Select(index => new GenerationChatMessage(index % 2 == 0, $"Message {index}",
                    new DateTime(2026, 11, 25), Array.Empty<GenerationReference>())).ToArray();
            GenerationItemViewModel source = await context.RestoreSavedChatAsync(new GenerationChatHistory(messages, includesCardTurn: true));
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 560d, 360d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                ScrollViewer scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                ScrollContentPresenter presenter = scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
                Border header = view.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-header"));
                Border chrome = view.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-history"));
                header.OpacityMask.Should().BeNull();
                (scroll.TranslatePoint(default, chrome) ?? throw new InvalidOperationException("The chat scroll position is missing."))
                    .Y.Should().BeApproximately(0d, 0.1d);
                scroll.Bounds.Height.Should().BeApproximately(chrome.Bounds.Height, 0.1d);

                scroll.Offset = Vector.Zero;
                window.CaptureRenderedFrame()?.Dispose();

                if (!overflowing)
                {
                    presenter.OpacityMask.Should().BeNull();
                    return;
                }

                LinearGradientBrush mask = presenter.OpacityMask.Should().BeOfType<LinearGradientBrush>().Subject;
                mask.StartPoint.Should().Be(new RelativePoint(0d, 0d, RelativeUnit.Relative));
                mask.EndPoint.Should().Be(new RelativePoint(0d, 1d, RelativeUnit.Relative));

                mask.GradientStops[0].Color.Should().Be(Colors.White);
                mask.GradientStops[3].Color.Should().Be(overflowing ? Colors.Transparent : Colors.White);

                if (overflowing)
                {
                    scroll.Offset = new Vector(0d, scroll.ScrollBarMaximum.Y / 2d);
                    window.CaptureRenderedFrame()?.Dispose();

                    mask.GradientStops[0].Color.Should().Be(Colors.White);
                    mask.GradientStops[3].Color.Should().Be(Colors.Transparent);
                    mask.GradientStops[1].Offset.Should().Be(0d);
                    mask.GradientStops[2].Offset.Should().BeLessThan(1d);
                    presenter.OpacityMask.Should().BeSameAs(mask);

                    scroll.ScrollToEnd();
                    window.CaptureRenderedFrame()?.Dispose();

                    mask.GradientStops[0].Color.Should().Be(Colors.White);
                    mask.GradientStops[3].Color.Should().Be(Colors.White);
                    presenter.OpacityMask.Should().BeNull();

                    scroll.Offset = Vector.Zero;
                    window.CaptureRenderedFrame()?.Dispose();
                }

                presenter.OpacityMask.Should().BeSameAs(mask);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(640, 240)]
    [InlineData(240, 640)]
    [InlineData(1697, 927)]
    public async Task ChatView_WithLateImageAboveViewport_KeepsScrollExtentAndReadingPosition(int width, int height)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            TaskCompletionSource decoding = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using GenerationReferencePreviewLoader loader = context.CreateDelayedPreviewLoader(decoding, ready.Task, width, height);
            GenerationReference image = new("late", "late.png", "image/png");
            GenerationChatMessage[] messages = Enumerable.Range(0, 60).Select(index =>
                new GenerationChatMessage(index == 20, $"Message {index}", new DateTime(2026, 11, 24),
                    index == 20 ? new GenerationReference[] { image } : Array.Empty<GenerationReference>())).ToArray();
            GenerationItemViewModel source = await context.RestoreSavedChatAsync(new GenerationChatHistory(messages, includesCardTurn: true));
            GenerationChatView view = new() { DataContext = context.Chat, PreviewLoader = loader };
            Window window = DesktopControlTestBase.Show(view, 560d, 480d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                window.CaptureRenderedFrame()?.Dispose();
                ScrollViewer scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                GenerationChatMessageView anchor = view.GetVisualDescendants().OfType<GenerationChatMessageView>().ElementAt(21);
                Point initial = anchor.TranslatePoint(default, scroll) ?? throw new InvalidOperationException();
                scroll.Offset = new Vector(0d, scroll.Offset.Y + initial.Y - 20d);
                window.CaptureRenderedFrame()?.Dispose();
                GenerationReferencePreviewControl preview = view.GetVisualDescendants().OfType<GenerationReferencePreviewControl>().Single();
                preview.LoadInViewport = false;
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => decoding.Task.IsCompleted);
                window.CaptureRenderedFrame()?.Dispose();
                double top = (anchor.TranslatePoint(default, scroll) ?? throw new InvalidOperationException()).Y;
                double extent = scroll.Extent.Height;
                double maximum = scroll.ScrollBarMaximum.Y;
                double offset = scroll.Offset.Y;
                scroll.CurrentAnchor.Should().NotBeNull();
                ready.SetResult();

                for (int frame = 0; frame < 30; frame++)
                {
                    await Task.Delay(16);
                    window.CaptureRenderedFrame()?.Dispose();
                    (anchor.TranslatePoint(default, scroll) ?? throw new InvalidOperationException()).Y.Should().BeApproximately(top, 1d);
                    scroll.Extent.Height.Should().BeApproximately(extent, 0.01d);
                    scroll.ScrollBarMaximum.Y.Should().BeApproximately(maximum, 0.01d);
                    scroll.Offset.Y.Should().BeApproximately(offset, 0.01d);
                }

                preview.Source.Should().NotBeNull();
                scroll.Offset.Y.Should().BeGreaterThan(0d);
                preview.LoadInViewport = true;
                scroll.ScrollToEnd();
                await WaitForFramesAsync(window, () => preview.Source is null);
                scroll.Extent.Height.Should().BeApproximately(extent, 0.01d);
                Point imageCenter = preview.TranslatePoint(new Point(0d, preview.Bounds.Height / 2d), scroll)
                    ?? throw new InvalidOperationException("The chat image has no position.");
                double imageOffset = scroll.Offset.Y + imageCenter.Y - scroll.Viewport.Height / 2d;
                scroll.Offset = new Vector(0d, imageOffset);
                await WaitForFramesAsync(window, () => preview.Source is not null);
                scroll.Extent.Height.Should().BeApproximately(extent, 0.01d);
                scroll.Offset.Y.Should().BeApproximately(imageOffset, 0.01d);
            }
            finally
            {
                ready.TrySetResult();
                window.Close();
            }
        });
    }
}
