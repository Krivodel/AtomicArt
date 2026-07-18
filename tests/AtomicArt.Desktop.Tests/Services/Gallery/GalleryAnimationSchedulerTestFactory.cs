using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

internal static class GalleryAnimationSchedulerTestFactory
{
    internal static GalleryAnimationScheduler Create(
        TestUiFrameScheduler frameScheduler,
        List<AppliedMotionFrame> appliedFrames)
    {
        ArgumentNullException.ThrowIfNull(frameScheduler);
        ArgumentNullException.ThrowIfNull(appliedFrames);

        return new GalleryAnimationScheduler(
            frameScheduler,
            (control, frame) =>
            {
                appliedFrames.Add(new AppliedMotionFrame(control, frame));
            });
    }
}
