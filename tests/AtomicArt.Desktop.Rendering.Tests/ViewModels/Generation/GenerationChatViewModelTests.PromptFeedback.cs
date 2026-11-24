using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.V)]
    [InlineData(Key.Back)]
    [InlineData(Key.Delete)]
    public async Task Prompt_WithVariantGroupAndNativeInput_ShowsRedFeedbackAndKeepsTheOriginalText(Key key)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            LocalCodexSettings settings = new();
            using UniversalNanoBananaPanelViewModel panel = UniversalNanoBananaPanelViewModelTests.CreateViewModel(
                imageModelOptionCatalog: new ImageModelOptionCatalog(settings), localCodexSettings: settings);
            panel.ConnectChat(context.Chat);
            await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
            GenerationChatBatch batch = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
            NanoBanana2PanelView view = new() { DataContext = panel };
            Window window = DesktopControlTestBase.Show(view, 700d, 520d);

            try
            {
                TextBox prompt = view.FindControl<TextBox>("PromptTextBox") ?? throw new InvalidOperationException();
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => prompt.IsReadOnly && prompt.IsEffectivelyEnabled);
                prompt.Focus().Should().BeTrue();
                string original = panel.Prompt;
                Point origin = prompt.TranslatePoint(default, window) ?? throw new InvalidOperationException();
                Rect region = new(origin, prompt.Bounds.Size);
                using SKBitmap baseline = CaptureRenderedBitmap(window);
                const double MinimumCoverage = 0.4d;
                double minimumPixels = region.Width * region.Height * MinimumCoverage;

                if (key == Key.A)
                {
                    window.KeyTextInput("a");
                }
                else
                {
                    window.KeyPress(key, key == Key.V ? RawInputModifiers.Control : RawInputModifiers.None, PhysicalKey.None, null);
                }

                try
                {
                    await GenerationChatViewModelTests.WaitForFramesAsync(window, () =>
                    {
                        using SKBitmap current = CaptureRenderedBitmap(window);

                        return GenerationChatViewModelTests.CountRedFeedbackPixels(baseline, current, region) >= minimumPixels;
                    });
                }
                finally
                {
                    GenerationChatViewModelTests.SaveChatFrame(window, $"locked-prompt-{key}.png");
                }

                panel.Prompt.Should().Be(original);
                prompt.Text.Should().Be(original);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () =>
                {
                    using SKBitmap current = CaptureRenderedBitmap(window);

                    return GenerationChatViewModelTests.CountRedFeedbackPixels(baseline, current, region) == 0;
                });
                await context.Chat.ReturnToSourceCommand.ExecuteAsync(null);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => !prompt.IsReadOnly);
                prompt.Focus();
                window.KeyTextInput("a");
                panel.Prompt.Should().NotBe(original);
            }
            finally
            {
                window.Close();

                foreach (GenerationChatRun run in batch.Runs)
                {
                    run.Complete(null);
                }
            }
        });
    }

    private static int CountRedFeedbackPixels(SKBitmap baseline, SKBitmap current, Rect region)
    {
        const int MinimumRedIncrease = 16;
        int count = 0;

        for (int y = Math.Max(0, (int)region.Top); y < Math.Min(current.Height, region.Bottom); y++)
        {
            for (int x = Math.Max(0, (int)region.Left); x < Math.Min(current.Width, region.Right); x++)
            {
                SKColor original = baseline.GetPixel(x, y);
                SKColor pixel = current.GetPixel(x, y);

                if ((pixel.Red - pixel.Green) - (original.Red - original.Green) > MinimumRedIncrease)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
