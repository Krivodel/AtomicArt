using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.Behaviors;

internal sealed class DragHoverState : IDisposable
{
    internal const string ActiveClass = "drag-hover";

    internal static TimeSpan Delay { get; } = TimeSpan.FromMilliseconds(HoverMilliseconds);

    private const int HoverMilliseconds = 600;

    private readonly Control _control;
    private readonly DispatcherTimer _timer;
    private readonly bool _previousAllowDrop;
    private bool _invoked;

    internal DragHoverState(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _previousAllowDrop = DragDrop.GetAllowDrop(control);
        _timer = new DispatcherTimer { Interval = DragHoverState.Delay };
        _timer.Tick += OnElapsed;
        DragDrop.SetAllowDrop(control, true);
        control.AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        control.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        control.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        control.AddHandler(DragDrop.DropEvent, OnDrop);
        control.DetachedFromVisualTree += OnDetached;
    }

    public void Dispose()
    {
        Cancel();
        _timer.Tick -= OnElapsed;
        _control.RemoveHandler(DragDrop.DragEnterEvent, OnDragOver);
        _control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        _control.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        _control.RemoveHandler(DragDrop.DropEvent, OnDrop);
        _control.DetachedFromVisualTree -= OnDetached;
        DragDrop.SetAllowDrop(_control, _previousAllowDrop);
    }

    private void Cancel()
    {
        _timer.Stop();
        _invoked = false;
        _control.Classes.Set(DragHoverState.ActiveClass, false);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        Point position = e.GetPosition(_control);

        if (!_control.IsEnabled || !_control.IsEffectivelyVisible
            || !new Rect(_control.Bounds.Size).Contains(position)
            || !ImageDropBehavior.AcceptsData(e.DataTransfer, ImageDropTargetKind.ImageAttachment))
        {
            Cancel();
            return;
        }

        _control.Classes.Set(DragHoverState.ActiveClass, true);
        e.DragEffects = DragDropEffects.Copy;
        ImageDropBehavior.MarkHandled(e, ImageDropTargetKind.ImageAttachment);

        if (!_invoked && !_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (!new Rect(_control.Bounds.Size).Contains(e.GetPosition(_control)))
        {
            Cancel();
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (_control.Classes.Contains(DragHoverState.ActiveClass))
        {
            ImageDropBehavior.MarkHandled(e, ImageDropTargetKind.ImageAttachment);
        }

        Cancel();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Cancel();
    }

    private void OnElapsed(object? sender, EventArgs e)
    {
        _timer.Stop();
        IRelayCommand? command = DragHoverBehavior.GetCommand(_control);

        if (_control.IsEnabled && _control.IsEffectivelyVisible && command?.CanExecute(null) == true)
        {
            _invoked = true;
            command.Execute(null);
        }
    }
}
