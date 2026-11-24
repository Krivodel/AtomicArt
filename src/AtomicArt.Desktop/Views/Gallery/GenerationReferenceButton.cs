using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Views.Gallery;

public sealed class GenerationReferenceButton : Button
{
    public IGenerationReferenceDragService? DragService
    {
        get => GetValue(DragServiceProperty);
        set => SetValue(DragServiceProperty, value);
    }
    public GenerationReference? DragReference
    {
        get => GetValue(DragReferenceProperty);
        set => SetValue(DragReferenceProperty, value);
    }

    public static readonly StyledProperty<IGenerationReferenceDragService?> DragServiceProperty =
        AvaloniaProperty.Register<GenerationReferenceButton, IGenerationReferenceDragService?>(nameof(DragService));
    public static readonly StyledProperty<GenerationReference?> DragReferenceProperty =
        AvaloniaProperty.Register<GenerationReferenceButton, GenerationReference?>(nameof(DragReference));

    protected override Type StyleKeyOverride => typeof(Button);

    private PointerPressedEventArgs? _pressed;
    private Point _dragOrigin;
    private IPointer? _draggingPointer;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (ResolveDragService() is not null && ResolveReference() is not null && _draggingPointer is null)
        {
            PointerPoint point = e.GetCurrentPoint(this);

            if (point.Properties.IsLeftButtonPressed)
            {
                e.Pointer.Capture(this);
                _pressed = e;
                _dragOrigin = point.Position;
            }
        }
    }

    protected override async void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_pressed is not { } pressed || pressed.Pointer != e.Pointer
            || ResolveDragService() is not { } dragService || ResolveReference() is not { } reference)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(this);

        if (!point.Properties.IsLeftButtonPressed)
        {
            _pressed = null;

            return;
        }

        if (!PointerDragThreshold.IsReached(_dragOrigin, point.Position))
        {
            return;
        }

        _pressed = null;
        _draggingPointer = e.Pointer;
        e.Handled = true;

        try
        {
            await dragService.DragAsync(this, pressed, reference, CancellationToken.None);
        }
        finally
        {
            ReleaseCapture(e.Pointer);
            _draggingPointer = null;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _pressed = null;

        if (_draggingPointer == e.Pointer)
        {
            e.Handled = true;
            ReleaseCapture(e.Pointer);

            return;
        }

        base.OnPointerReleased(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _pressed = null;
        base.OnPointerCaptureLost(e);
    }

    private void ReleaseCapture(IPointer pointer)
    {
        if (pointer.Captured == this)
        {
            pointer.Capture(null);
        }
    }

    private IGenerationReferenceDragService? ResolveDragService()
    {
        return DragService ?? AttachmentImageDragBehavior.GetReferenceDragService(this);
    }

    private GenerationReference? ResolveReference()
    {
        return DragReference ?? CommandParameter as GenerationReference;
    }
}
