using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Dialogs;
using AtomicArt.Desktop.Views.Dialogs;

namespace AtomicArt.Desktop.Tests.Views.Dialogs;

public sealed class ErrorDialogOverlayViewTests : AnimatedGalleryControlTestBase
{
    private const double ExpectedOverlayWidth = 480d;
    private const string ErrorMessage = "The server is unavailable.";

    [Fact]
    public void ErrorDialogOverlayView_WhenShown_UsesSharedModalWithCopyAction()
    {
        Dispatch(() =>
        {
            ErrorDialogViewModel viewModel = new(
                new RecordingTextClipboardService(),
                new TestViewModelErrorHandler());
            viewModel.Open(ErrorMessage);
            ErrorDialogOverlayView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                window.CaptureRenderedFrame();
                ModalOverlayControl overlay = view
                    .GetVisualDescendants()
                    .OfType<ModalOverlayControl>()
                    .Single();
                Button copyButton = view
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => object.Equals(
                        button.Content,
                        TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.Copy)));

                overlay.Title.Should().Be(TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.Error));
                overlay.Bounds.Width.Should().Be(ExpectedOverlayWidth);
                overlay.Bounds.Height.Should().BeLessThan(window.Bounds.Height);
                copyButton.IsVisible.Should().BeTrue();
                view.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Should()
                    .ContainSingle(textBlock => textBlock.Text == ErrorMessage);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
