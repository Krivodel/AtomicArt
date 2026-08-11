using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

using AtomicArt.Desktop.Controls.Generation;

namespace AtomicArt.Desktop.Behaviors;

internal static class DropOverlayState
{
    private const int HideDelayMilliseconds = 50;

    private static readonly AttachedProperty<int> RevisionProperty =
        AvaloniaProperty.RegisterAttached<Control, int>(
            "DropOverlayRevision",
            typeof(DropOverlayState));

    internal static void SetActive(
        Control target,
        ImageDropOverlayControl? overlay,
        bool isActive)
    {
        ArgumentNullException.ThrowIfNull(target);

        CancelScheduledHide(target);

        if (overlay is not null)
        {
            overlay.IsActive = isActive;
        }
    }

    internal static void ScheduleHide(
        Control target,
        ImageDropOverlayControl? overlay)
    {
        ArgumentNullException.ThrowIfNull(target);

        CancelScheduledHide(target);

        if (overlay is null)
        {
            return;
        }

        int scheduledRevision = target.GetValue(RevisionProperty);

        DispatcherTimer.RunOnce(
            () =>
            {
                if (target.GetValue(RevisionProperty) == scheduledRevision)
                {
                    overlay.IsActive = false;
                }
            },
            TimeSpan.FromMilliseconds(HideDelayMilliseconds),
            DispatcherPriority.Input);
    }

    internal static void CancelScheduledHide(Control target)
    {
        ArgumentNullException.ThrowIfNull(target);

        int revision = target.GetValue(RevisionProperty);
        target.SetValue(RevisionProperty, revision + 1);
    }

    internal static void HandleDragLeave(
        Control target,
        Control dropArea,
        ImageDropOverlayControl? overlay,
        DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dropArea);
        ArgumentNullException.ThrowIfNull(e);

        Point position = e.GetPosition(dropArea);
        bool isOutside = position.X < 0d
            || position.Y < 0d
            || position.X > dropArea.Bounds.Width
            || position.Y > dropArea.Bounds.Height;

        if (isOutside)
        {
            SetActive(target, overlay, false);
            return;
        }

        ScheduleHide(target, overlay);
    }
}
