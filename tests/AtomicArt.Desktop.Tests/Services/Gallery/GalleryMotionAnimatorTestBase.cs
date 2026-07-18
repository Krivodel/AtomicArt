using Avalonia.Controls;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public abstract class GalleryMotionAnimatorTestBase
{
    private protected static GalleryMotionAnimator CreateAnimator(GalleryAnimationScheduler animationScheduler)
    {
        GalleryOverlayEffects overlayEffects = new(animationScheduler);
        GalleryLayoutService galleryLayout = new();

        return GalleryMotionAnimatorTestFactory.Create(
            animationScheduler,
            overlayEffects,
            galleryLayout);
    }

    private protected static GalleryFrontGenerationRunState CreateFrontState(IReadOnlyList<object> activeItems)
    {
        GalleryFrontGenerationRunState state = new(new List<GalleryOperation>());
        state.ActiveFrontItems.AddRange(activeItems);

        return state;
    }

    private protected static GalleryOperationCoordinator CreateContext(TestUiFrameScheduler frameScheduler)
    {
        GalleryOperationCoordinator context = GalleryOperationCoordinatorTestFactory.Create(
            frameScheduler,
            new GalleryOperationRunnerRegistry(new List<IGalleryOperationRunner>()));
        context.AttachScene(
            new ScrollViewer(),
            new Canvas(),
            new Canvas(),
            new List<object>(),
            item => (Guid)item,
            _ => new Border(),
            () => Task.CompletedTask);

        return context;
    }

    private protected static List<object> CreatePositionedItems(
        GalleryOperationCoordinator context,
        int count)
    {
        List<object> items = [];
        for (int i = 0; i < count; i++)
        {
            Guid id = Guid.NewGuid();
            Border control = CreatePositionedControl(i * (GalleryLayoutService.CardWidth + GalleryLayoutService.CardGap), 0d);
            context.CardControls[id] = control;
            items.Add(id);
        }

        return items;
    }

    private protected static Border CreatePositionedControl(double left, double top)
    {
        Border control = new()
        {
            Width = GalleryLayoutService.CardWidth,
            Height = GalleryLayoutService.CardHeight
        };
        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);

        return control;
    }

    private protected static MotionFrame Interpolate(IReadOnlyList<MotionFrame> frames, double progress)
    {
        double scaled = progress * (frames.Count - 1);
        int index = Math.Min((int)Math.Floor(scaled), frames.Count - 2);
        double local = scaled - index;
        MotionFrame from = frames[index];
        MotionFrame to = frames[index + 1];

        return new MotionFrame(
            Lerp(from.X, to.X, local),
            Lerp(from.Y, to.Y, local),
            Lerp(from.Scale, to.Scale, local),
            Lerp(from.Rotate, to.Rotate, local),
            Lerp(from.Opacity, to.Opacity, local));
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + ((to - from) * amount);
    }

}
