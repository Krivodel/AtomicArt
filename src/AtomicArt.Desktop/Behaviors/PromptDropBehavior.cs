using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Behaviors;

public static class PromptDropBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsEnabled",
            typeof(PromptDropBehavior));

    public static readonly AttachedProperty<IRelayCommand<string?>?> ReplacePromptCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, IRelayCommand<string?>?>(
            "ReplacePromptCommand",
            typeof(PromptDropBehavior),
            inherits: true);

    public static readonly AttachedProperty<ImageDropOverlayControl?> OverlayProperty =
        AvaloniaProperty.RegisterAttached<Control, ImageDropOverlayControl?>(
            "Overlay",
            typeof(PromptDropBehavior));

    static PromptDropBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, IsEnabledProperty);
    }

    public static void SetIsEnabled(Control control, bool value)
    {
        AttachedPropertyValueAccessor.Set(control, IsEnabledProperty, value);
    }

    public static IRelayCommand<string?>? GetReplacePromptCommand(Control control)
    {
        return AttachedPropertyValueAccessor.Get(
            control,
            ReplacePromptCommandProperty);
    }

    public static void SetReplacePromptCommand(
        Control control,
        IRelayCommand<string?>? value)
    {
        AttachedPropertyValueAccessor.Set(
            control,
            ReplacePromptCommandProperty,
            value);
    }

    public static ImageDropOverlayControl? GetOverlay(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, OverlayProperty);
    }

    public static void SetOverlay(
        Control control,
        ImageDropOverlayControl? value)
    {
        AttachedPropertyValueAccessor.Set(control, OverlayProperty, value);
    }

    private static void UpdateDragState(object? sender, DragEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        string? prompt = AtomicArtPromptDragData.GetPromptOrDefault(
            e.DataTransfer);

        if (prompt is null)
        {
            SetOverlayActive(control, false);
            return;
        }

        IRelayCommand<string?>? command = GetReplacePromptCommand(control);
        bool acceptsPrompt = command?.CanExecute(prompt) == true;

        SetOverlayActive(control, acceptsPrompt);
        e.DragEffects = acceptsPrompt
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static void SetOverlayActive(Control control, bool isActive)
    {
        DropOverlayState.SetActive(control, GetOverlay(control), isActive);
    }

    private static void OnIsEnabledChanged(
        Control control,
        AvaloniaPropertyChangedEventArgs args)
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
                DragDrop.DragOverEvent,
                OnDragOver,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            control.AddHandler(
                DragDrop.DragLeaveEvent,
                OnDragLeave,
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
        control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        control.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
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

        DropOverlayState.HandleDragLeave(
            control,
            control,
            GetOverlay(control),
            e);
    }

    private static void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        SetOverlayActive(control, false);

        string? prompt = AtomicArtPromptDragData.GetPromptOrDefault(
            e.DataTransfer);

        if (prompt is null)
        {
            return;
        }

        IRelayCommand<string?>? command = GetReplacePromptCommand(control);

        e.Handled = true;

        if (command?.CanExecute(prompt) == true)
        {
            command.Execute(prompt);
        }
    }
}
