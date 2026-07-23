using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.UiAnimation;

internal static class UiAnimationSchedulerTestFactory
{
    internal static UiAnimationScheduler Create(
        TestUiFrameScheduler frameScheduler,
        List<AppliedMotionFrame> appliedFrames)
    {
        ArgumentNullException.ThrowIfNull(frameScheduler);
        ArgumentNullException.ThrowIfNull(appliedFrames);

        return new UiAnimationScheduler(
            frameScheduler,
            (control, frame) =>
            {
                appliedFrames.Add(new AppliedMotionFrame(control, frame));
            });
    }
}
