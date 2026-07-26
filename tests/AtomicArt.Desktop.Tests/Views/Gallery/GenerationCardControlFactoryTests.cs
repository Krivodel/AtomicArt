using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;
using Avalonia.Media.Imaging;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services.Gallery;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GenerationCardControlFactoryTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void Create_AfterTransientControlWasCreated_ReusesRecycledControl()
    {
        Dispatch(() =>
        {
            StubGalleryPreviewBitmapLoader loader =
                new((_, _) => Task.FromResult<Bitmap?>(null));
            using GalleryPreviewBitmapProvider provider = new(
                loader,
                NullLogger<GalleryPreviewBitmapProvider>.Instance);
            UiAnimationScheduler animationScheduler =
                new(new DiscardingUiFrameScheduler());
            GalleryPreviewSourceScheduler sourceScheduler =
                new(new DiscardingUiFrameScheduler());
            GenerationCardControlFactory factory = new(
                provider,
                sourceScheduler,
                animationScheduler);
            GalleryCardCommands commands = new(null, null, null, null);
            StandaloneGenerationPreviewExpansionHost expansionHost = new(new Border());
            Control firstControl = factory.Create(
                new object(),
                commands,
                expansionHost);
            firstControl.IsVisible = true;
            factory.Recycle(firstControl);
            firstControl.IsVisible.Should().BeFalse();

            Control transientControl = factory.CreateTransient(
                new object(),
                commands,
                expansionHost);
            Control secondControl = factory.Create(
                new object(),
                commands,
                expansionHost);

            transientControl.Should().NotBeSameAs(firstControl);
            secondControl.Should().BeSameAs(firstControl);
            secondControl.IsVisible.Should().BeTrue();
        });
    }
}
