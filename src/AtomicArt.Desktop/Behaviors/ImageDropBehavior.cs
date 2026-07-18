using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Behaviors;

public static class ImageDropBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsEnabled",
            typeof(ImageDropBehavior));

    public static readonly AttachedProperty<IDragDropImageService?> DragDropImageServiceProperty =
        AvaloniaProperty.RegisterAttached<Control, IDragDropImageService?>(
            "DragDropImageService",
            typeof(ImageDropBehavior),
            inherits: true);

    public static readonly AttachedProperty<Control?> DropAreaProperty =
        AvaloniaProperty.RegisterAttached<Control, Control?>(
            "DropArea",
            typeof(ImageDropBehavior));

    public static readonly AttachedProperty<ImageDropOverlayControl?> OverlayProperty =
        AvaloniaProperty.RegisterAttached<Control, ImageDropOverlayControl?>(
            "Overlay",
            typeof(ImageDropBehavior));

    public static readonly AttachedProperty<ImageDropTargetKind> TargetKindProperty =
        AvaloniaProperty.RegisterAttached<Control, ImageDropTargetKind>(
            "TargetKind",
            typeof(ImageDropBehavior),
            defaultValue: ImageDropTargetKind.ExternalFiles);

    private const int DragLeaveHideDelayMilliseconds = 50;

    private static readonly AttachedProperty<int> OverlayRevisionProperty =
        AvaloniaProperty.RegisterAttached<Control, int>(
            "OverlayRevision",
            typeof(ImageDropBehavior));

    static ImageDropBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(IsEnabledProperty, value);
    }

    public static IDragDropImageService? GetDragDropImageService(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(DragDropImageServiceProperty);
    }

    public static void SetDragDropImageService(Control control, IDragDropImageService? value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(DragDropImageServiceProperty, value);
    }

    public static Control? GetDropArea(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(DropAreaProperty);
    }

    public static void SetDropArea(Control control, Control? value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(DropAreaProperty, value);
    }

    public static ImageDropOverlayControl? GetOverlay(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(OverlayProperty);
    }

    public static void SetOverlay(Control control, ImageDropOverlayControl? value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(OverlayProperty, value);
    }

    public static ImageDropTargetKind GetTargetKind(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(TargetKindProperty);
    }

    public static void SetTargetKind(Control control, ImageDropTargetKind value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(TargetKindProperty, value);
    }

    internal static bool AcceptsData(
        IDataTransfer dataTransfer,
        ImageDropTargetKind targetKind)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        if (!dataTransfer.Contains(DataFormat.File))
        {
            return false;
        }

        bool isGalleryImage = GalleryImageDragData.IsGalleryImage(dataTransfer);

        return targetKind switch
        {
            ImageDropTargetKind.ExternalFiles => !isGalleryImage,
            ImageDropTargetKind.GalleryImage => isGalleryImage,
            _ => false
        };
    }

    internal static void ScheduleOverlayHide(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        CancelScheduledOverlayHide(control);
        int scheduledRevision = control.GetValue(OverlayRevisionProperty);

        DispatcherTimer.RunOnce(
            () =>
            {
                if (control.GetValue(OverlayRevisionProperty) != scheduledRevision)
                {
                    return;
                }

                ImageDropOverlayControl? overlay = GetOverlay(control);

                if (overlay is not null)
                {
                    overlay.IsActive = false;
                }
            },
            TimeSpan.FromMilliseconds(DragLeaveHideDelayMilliseconds),
            DispatcherPriority.Input);
    }

    internal static void CancelScheduledOverlayHide(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        int revision = control.GetValue(OverlayRevisionProperty);
        control.SetValue(OverlayRevisionProperty, revision + 1);
    }

    private static void UpdateDragState(object? sender, DragEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        ImageDropTargetKind targetKind = GetTargetKind(control);

        if (IsHandledByGalleryTarget(e, targetKind))
        {
            SetOverlayActive(control, false);
            return;
        }

        bool acceptsData = AcceptsData(e.DataTransfer, targetKind)
            && IsInsideDropArea(control, e);
        SetOverlayActive(control, acceptsData);

        if (acceptsData)
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        HandleRejectedGalleryDrag(e, targetKind);
    }

    private static void HandleRejectedGalleryDrag(
        DragEventArgs e,
        ImageDropTargetKind targetKind)
    {
        bool rejectsGalleryImage = targetKind == ImageDropTargetKind.ExternalFiles
            && e.DataTransfer.Contains(DataFormat.File)
            && GalleryImageDragData.IsGalleryImage(e.DataTransfer);

        if (rejectsGalleryImage)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    private static bool IsHandledByGalleryTarget(
        DragEventArgs e,
        ImageDropTargetKind targetKind)
    {
        return e.Handled
            && targetKind == ImageDropTargetKind.ExternalFiles
            && e.DataTransfer.Contains(DataFormat.File)
            && GalleryImageDragData.IsGalleryImage(e.DataTransfer);
    }

    private static bool IsInsideDropArea(Control control, DragEventArgs e)
    {
        Control dropArea = GetDropArea(control) ?? control;
        Point position = e.GetPosition(dropArea);

        return position is { X: >= 0d, Y: >= 0d }
               && position.X <= dropArea.Bounds.Width
               && position.Y <= dropArea.Bounds.Height;
    }

    private static void SetOverlayActive(Control control, bool isActive)
    {
        CancelScheduledOverlayHide(control);
        ImageDropOverlayControl? overlay = GetOverlay(control);

        if (overlay is not null)
        {
            overlay.IsActive = isActive;
        }
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        bool isEnabled = args.NewValue is true;
        DragDrop.SetAllowDrop(control, isEnabled);

        if (isEnabled)
        {
            control.AddHandler(
                DragDrop.DragEnterEvent,
                OnDragEnter,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            control.AddHandler(
                DragDrop.DragLeaveEvent,
                OnDragLeave,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            control.AddHandler(
                DragDrop.DragOverEvent,
                OnDragOver,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            control.AddHandler(
                DragDrop.DropEvent,
                OnDrop,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            return;
        }

        control.RemoveHandler(DragDrop.DragEnterEvent, OnDragEnter);
        control.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        control.RemoveHandler(DragDrop.DropEvent, OnDrop);
        SetOverlayActive(control, false);
    }

    private static void OnDragEnter(object? sender, DragEventArgs e)
    {
        UpdateDragState(sender, e);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        UpdateDragState(sender, e);
    }

    private static void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        Control dropArea = GetDropArea(control) ?? control;
        Point position = e.GetPosition(dropArea);
        bool isOutside = position.X < 0d
            || position.Y < 0d
            || position.X > dropArea.Bounds.Width
            || position.Y > dropArea.Bounds.Height;

        if (isOutside)
        {
            SetOverlayActive(control, false);
            return;
        }

        ScheduleOverlayHide(control);
    }

    private static async void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        SetOverlayActive(control, false);
        ImageDropTargetKind targetKind = GetTargetKind(control);

        if (IsHandledByGalleryTarget(e, targetKind))
        {
            return;
        }

        if (!AcceptsData(e.DataTransfer, targetKind) || !IsInsideDropArea(control, e))
        {
            HandleRejectedGalleryDrag(e, targetKind);
            return;
        }

        e.Handled = true;
        IDragDropImageService? dragDropImageService = GetDragDropImageService(control);
        int maxInputBytes = ImageAttachmentBehavior.GetMaxInputBytes(control);

        if (dragDropImageService is null || maxInputBytes <= 0)
        {
            return;
        }

        try
        {
            IReadOnlyList<ImageAttachmentInput> inputs = await dragDropImageService
                .ExtractImagesAsync(e.DataTransfer, maxInputBytes, CancellationToken.None);

            await ImageAttachmentBehavior.TryAttachAsync(control, inputs);
        }
        catch (Exception ex)
        {
            ImageAttachmentBehavior.HandleError(control, ex);
        }
    }
}
