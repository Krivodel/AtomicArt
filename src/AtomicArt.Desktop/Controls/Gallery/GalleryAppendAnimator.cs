using Avalonia.Controls;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryAppendAnimator
{
    private readonly GalleryAnimationScheduler _animationScheduler;

    public GalleryAppendAnimator(GalleryAnimationScheduler animationScheduler)
    {
        _animationScheduler = animationScheduler ?? throw new ArgumentNullException(nameof(animationScheduler));
    }

    public async Task AnimateAppendBatchAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<object> batch)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(batch);

        List<Task> animations = [];
        for (int i = 0; i < batch.Count; i++)
        {
            Guid id = context.GetItemId(batch[i]);
            if (!context.CardControls.TryGetValue(id, out Control? control))
            {
                continue;
            }

            animations.Add(_animationScheduler.AnimateAsync(
                control,
                new List<MotionFrame>
                {
                    new(0d, 14d, 0.96d, 0d, 0d),
                    new(0d, 0d, 1d, 0d, 1d)
                },
                AnimationTiming.ScaleTime(360, GalleryMotionTimings.SpawnSpeed),
                AnimationTiming.ScaleTime(i * 28, GalleryMotionTimings.SpawnSpeed),
                MotionEasing.EaseOut));
        }

        await Task.WhenAll(animations);
    }
}
