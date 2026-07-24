using Avalonia.Headless;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GenerationPreviewControlTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void GenerationProgress_WhenGenerationFails_StopsPixelAnimation()
    {
        Dispatch(() =>
        {
            GenerationItemDto item = GenerationItemDtoTestFactory.Create(
                status: GenerationItemStatus.Generating);
            GenerationItemViewModel viewModel = new(
                item,
                0,
                null,
                GenerationItemStatusDescriptorRegistryTestFactory.Create());
            GenerationPreviewControl control = new()
            {
                DataContext = viewModel
            };

            Show(control, 220d, 220d, window =>
            {
                AttachmentPixelLoadingControl indicator = control
                    .GetVisualDescendants()
                    .OfType<AttachmentPixelLoadingControl>()
                    .Single();

                indicator.GridSize.Should().Be(16);
                indicator.IsActive.Should().BeTrue();

                viewModel.MarkFailed();
                window.CaptureRenderedFrame();

                indicator.IsActive.Should().BeFalse();
            });
        });
    }
}
