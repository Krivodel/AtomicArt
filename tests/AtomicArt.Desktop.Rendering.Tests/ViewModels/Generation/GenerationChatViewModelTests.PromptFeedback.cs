using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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
    [Fact]
    public async Task Prompt_WithVariantGroup_AllowsNativeInputWithoutFeedbackAndDisablesGeneration()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            LocalCodexSettings settings = new();
            using UniversalNanoBananaPanelViewModel panel = UniversalNanoBananaPanelViewModelTests.CreateViewModel(
                imageModelOptionCatalog: new ImageModelOptionCatalog(settings), localCodexSettings: settings);
            await panel.LoadModelCatalogCommand.ExecuteAsync(null);
            panel.ConnectChat(context.Chat);
            await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
            GenerationChatBatch batch = await SubmitParallelAsync(context.Chat);
            panel.Prompt = "Unsent draft";
            NanoBanana2PanelView view = new() { DataContext = panel };
            Window window = Show(view, 700d, 520d);

            try
            {
                TextBox prompt = view.FindControl<TextBox>("PromptTextBox") ?? throw new InvalidOperationException();
                await WaitForFramesAsync(window, () => prompt.IsEffectivelyEnabled);
                prompt.IsReadOnly.Should().BeFalse();
                prompt.Focus().Should().BeTrue();
                prompt.CaretIndex = panel.Prompt.Length;
                Border background = NanoBanana2PanelView.FindBackgroundFlashTarget(prompt)
                    ?? throw new InvalidOperationException("The prompt background is missing.");
                Avalonia.Media.IBrush? originalBackground = background.Background;
                Button generate = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => object.ReferenceEquals(button.Command, panel.GenerateCommand));
                Button addVariants = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => object.ReferenceEquals(button.Command, panel.AddVariantsCommand));

                window.KeyTextInput(" next");
                window.CaptureRenderedFrame()?.Dispose();

                panel.Prompt.Should().Be("Unsent draft next");
                prompt.Text.Should().Be(panel.Prompt);
                background.Background.Should().BeSameAs(originalBackground);
                panel.RequiresChatBranchSelection.Should().BeTrue();
                panel.GenerateCommand.CanExecute(null).Should().BeFalse();
                generate.IsEffectivelyEnabled.Should().BeFalse();
                generate.Content.Should().Be("Choose a branch");
                addVariants.IsEffectivelyVisible.Should().BeTrue();
                addVariants.IsEffectivelyEnabled.Should().BeTrue();
                addVariants.Content.Should().Be("More variants");
                generate.Bounds.Right.Should().BeLessThanOrEqualTo(addVariants.Bounds.Left);
                await context.Chat.ReturnToSourceCommand.ExecuteAsync(null);
                await WaitForFramesAsync(window, () => generate.IsEffectivelyEnabled);
                panel.Prompt.Should().Be("Unsent draft next");
                generate.Content.Should().Be(panel.Quote.GenerateButtonText);
                addVariants.IsEffectivelyVisible.Should().BeFalse();
                panel.SelectedModel = panel.AvailableModels.First(model => !LocalCodexImageModel.Matches(model.Id));
                string previousLabel = panel.GenerateButtonText;
                panel.GenerationCount = panel.GenerationCount == 1 ? 2 : 1;
                window.CaptureRenderedFrame()?.Dispose();
                generate.Content.Should().Be(panel.Quote.GenerateButtonText);
                panel.GenerateButtonText.Should().NotBe(previousLabel);
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
