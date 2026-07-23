using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

internal static class AnimatedGallerySceneTestFactory
{
    internal static AnimatedGalleryScene Create(TopLevel topLevel)
    {
        return Create(topLevel, null);
    }

    internal static AnimatedGalleryScene Create(
        TopLevel topLevel,
        IUiFrameScheduler? frameSchedulerOverride)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        IUiFrameScheduler frameScheduler = frameSchedulerOverride
            ?? new AvaloniaUiFrameSchedulerFactory().Create(topLevel);
        GalleryLayoutService galleryLayout = new();
        UiAnimationScheduler animationScheduler = new(frameScheduler);
        GalleryOverlayEffects overlayEffects = new(animationScheduler);
        GalleryMotionAnimator motionAnimator = GalleryMotionAnimatorTestFactory.Create(
            animationScheduler,
            overlayEffects,
            galleryLayout);
        List<IGalleryOperationRunner> runners =
        [
            new GalleryAppendRunner(
                motionAnimator,
                galleryLayout,
                NullLogger<GalleryAppendRunner>.Instance),
            new GalleryFrontGenerationRunner(
                animationScheduler,
                motionAnimator,
                galleryLayout,
                NullLogger<GalleryFrontGenerationRunner>.Instance,
                new GalleryFrontGenerationRetargetWaiter(animationScheduler)),
            new GalleryRemoveRunner(
                animationScheduler,
                motionAnimator,
                galleryLayout,
                NullLogger<GalleryRemoveRunner>.Instance),
            new GalleryMixedMutationRunner(
                motionAnimator,
                galleryLayout,
                NullLogger<GalleryMixedMutationRunner>.Instance)
        ];
        IGalleryOperationRunnerRegistry runnerRegistry =
            new GalleryOperationRunnerRegistry(runners);
        GalleryOperationCoordinator operationCoordinator =
            GalleryOperationCoordinatorTestFactory.Create(
                frameScheduler,
                runnerRegistry);

        return new AnimatedGalleryScene(
            galleryLayout,
            animationScheduler,
            motionAnimator,
            operationCoordinator,
            new GenerationCardControlFactory(),
            NullLogger<AnimatedGalleryResizeController>.Instance);
    }
}
