using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

internal sealed class GalleryMotionTestScene
{
    public TestUiFrameScheduler FrameScheduler { get; }
    public List<AppliedMotionFrame> AppliedFrames { get; }
    public GalleryMotionAnimator Animator { get; }
    public GalleryOperationCoordinator Context { get; }

    private GalleryMotionTestScene(
        TestUiFrameScheduler frameScheduler,
        List<AppliedMotionFrame> appliedFrames,
        GalleryMotionAnimator animator,
        GalleryOperationCoordinator context)
    {
        FrameScheduler = frameScheduler;
        AppliedFrames = appliedFrames;
        Animator = animator;
        Context = context;
    }

    public static GalleryMotionTestScene Create()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        GalleryAnimationScheduler animationScheduler =
            GalleryAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        GalleryOverlayEffects overlayEffects = new(animationScheduler);
        GalleryLayoutService galleryLayout = new();
        GalleryMotionAnimator animator = GalleryMotionAnimatorTestFactory.Create(
            animationScheduler,
            overlayEffects,
            galleryLayout);
        GalleryOperationCoordinator context = GalleryOperationCoordinatorTestFactory.CreateAttached(
            frameScheduler,
            new List<object>());

        return new GalleryMotionTestScene(
            frameScheduler,
            appliedFrames,
            animator,
            context);
    }
}
