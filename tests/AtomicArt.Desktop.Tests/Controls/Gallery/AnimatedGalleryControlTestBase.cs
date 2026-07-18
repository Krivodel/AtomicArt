using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;
using AtomicArt.Desktop.Tests.Services.Gallery;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Tests.Avalonia;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public abstract class AnimatedGalleryControlTestBase
{
    private static readonly Guid ItemId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<AnimatedGalleryControlTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    private protected static void Dispatch(Action action)
    {
        HeadlessTestSessionDispatcher.Dispatch(
            typeof(AnimatedGalleryControlTestBase),
            SessionLock,
            action);
    }

    private protected static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(AnimatedGalleryControlTestBase),
            SessionLock,
            action);
    }

    private protected static IAnimatedGallerySceneFactory CreateSceneFactory()
    {
        return CreateSceneFactory(null);
    }

    private protected static IAnimatedGallerySceneFactory CreateSceneFactory(IUiFrameScheduler? frameScheduler)
    {
        GallerySceneServicesFactory servicesFactory = new(
            topLevel => AnimatedGallerySceneTestFactory.Create(topLevel, frameScheduler));

        return new AnimatedGallerySceneFactory(servicesFactory);
    }

    private protected static Window Show(Control control)
    {
        return Show(control, 640d, 640d);
    }

    private protected static Window Show(Control control, double width, double height)
    {
        Window window = new()
        {
            Width = width,
            Height = height,
            Content = control
        };

        window.Show();
        window.CaptureRenderedFrame();

        return window;
    }

    private protected static Canvas GetGalleryPanel(AnimatedGalleryControl control)
    {
        ScrollViewer scrollViewer = GetGalleryScrollViewer(control);

        if (scrollViewer.Content is not Canvas galleryPanel)
        {
            throw new InvalidOperationException("Animated gallery panel was not found.");
        }

        return galleryPanel;
    }

    private protected static ScrollViewer GetGalleryScrollViewer(AnimatedGalleryControl control)
    {
        return GetRootGrid(control)
            .Children
            .OfType<ScrollViewer>()
            .Single();
    }

    private protected static Canvas GetOverlayCanvas(AnimatedGalleryControl control)
    {
        return GetRootGrid(control)
            .Children
            .OfType<Canvas>()
            .Single();
    }

    private protected static TranslateTransform GetTranslateTransform(Control control)
    {
        if (control.RenderTransform is not TransformGroup transformGroup)
        {
            throw new InvalidOperationException("Animated card transform was not found.");
        }

        return transformGroup.Children
            .OfType<TranslateTransform>()
            .Single();
    }

    private protected static GenerationItemViewModel CreateItem()
    {
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            modelId: "test-model",
            modelDisplayName: "Test Model",
            aspectRatio: "1:1",
            createdAtUtc: CreatedAtUtc);

        return new GenerationItemViewModel(
            item,
            0,
            null,
            GenerationItemStatusDescriptorRegistryTestFactory.Create());
    }

    private static Grid GetRootGrid(AnimatedGalleryControl control)
    {
        if (control.Content is not Grid root)
        {
            throw new InvalidOperationException("Animated gallery root grid was not found.");
        }

        return root;
    }
}
