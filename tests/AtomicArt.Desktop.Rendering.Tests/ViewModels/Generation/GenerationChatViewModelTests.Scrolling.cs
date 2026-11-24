using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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
    [InlineData(640, 240)]
    [InlineData(240, 640)]
    public async Task ChatView_WithLateImageAboveViewport_KeepsReadingPosition(int width, int height)
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
                scroll.CurrentAnchor.Should().NotBeNull();
                ready.SetResult();

                for (int frame = 0; frame < 30; frame++)
                {
                    await Task.Delay(16);
                    window.CaptureRenderedFrame()?.Dispose();
                    (anchor.TranslatePoint(default, scroll) ?? throw new InvalidOperationException()).Y.Should().BeApproximately(top, 1d);
                }

                preview.Source.Should().NotBeNull();
                scroll.Offset.Y.Should().BeGreaterThan(0d);
            }
            finally
            {
                ready.TrySetResult();
                window.Close();
            }
        });
    }
}
