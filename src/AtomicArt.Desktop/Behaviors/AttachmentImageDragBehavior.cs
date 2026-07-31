using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Behaviors;

public static class AttachmentImageDragBehavior
{
    public static readonly AttachedProperty<Control?> DragBoundaryProperty =
        AvaloniaProperty.RegisterAttached<Control, Control?>(
            "DragBoundary",
            typeof(AttachmentImageDragBehavior),
            inherits: true);
    public static readonly AttachedProperty<IAttachmentImageDragService?> DragServiceProperty =
        AvaloniaProperty.RegisterAttached<Control, IAttachmentImageDragService?>(
            "DragService",
            typeof(AttachmentImageDragBehavior),
            inherits: true);

    public static Control? GetDragBoundary(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, DragBoundaryProperty);
    }

    public static IAttachmentImageDragService? GetDragService(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, DragServiceProperty);
    }

    public static void SetDragBoundary(Control control, Control? value)
    {
        AttachedPropertyValueAccessor.Set(control, DragBoundaryProperty, value);
    }

    public static void SetDragService(
        Control control,
        IAttachmentImageDragService? value)
    {
        AttachedPropertyValueAccessor.Set(control, DragServiceProperty, value);
    }
}
