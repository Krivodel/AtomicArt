using SukiUI.Controls;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private void StartFrameAnimation(
        TimeSpan duration,
        Func<bool> isCurrent,
        Action<double> applyFrame,
        Action? cancelled = null,
        Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(isCurrent);
        ArgumentNullException.ThrowIfNull(applyFrame);

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        RequestAnimationFrame(OnFrame);

        void OnFrame(TimeSpan frameTime)
        {
            _ = frameTime;

            if (!isCurrent())
            {
                cancelled?.Invoke();
                return;
            }

            double elapsed = (DateTimeOffset.UtcNow - startedAt).TotalSeconds;
            double progress = Math.Clamp(elapsed / duration.TotalSeconds, 0d, 1d);
            applyFrame(progress);

            if (progress < 1d)
            {
                RequestAnimationFrame(OnFrame);
                return;
            }

            completed?.Invoke();
        }
    }
}
