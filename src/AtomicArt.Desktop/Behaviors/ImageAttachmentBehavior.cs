using Avalonia;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Behaviors;

public static class ImageAttachmentBehavior
{
    public static readonly AttachedProperty<int> MaxInputBytesProperty =
        AvaloniaProperty.RegisterAttached<Control, int>(
            "MaxInputBytes",
            typeof(ImageAttachmentBehavior),
            inherits: true);

    public static readonly AttachedProperty<IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>?>
        AttachImagesCommandProperty =
            AvaloniaProperty.RegisterAttached<
                Control,
                IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>?>(
                "AttachImagesCommand",
                typeof(ImageAttachmentBehavior),
                inherits: true);

    public static readonly AttachedProperty<IRelayCommand<Exception?>?> ErrorCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, IRelayCommand<Exception?>?>(
            "ErrorCommand",
            typeof(ImageAttachmentBehavior),
            inherits: true);

    public static int GetMaxInputBytes(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(MaxInputBytesProperty);
    }

    public static void SetMaxInputBytes(Control control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(MaxInputBytesProperty, value);
    }

    public static IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? GetAttachImagesCommand(
        Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(AttachImagesCommandProperty);
    }

    public static void SetAttachImagesCommand(
        Control control,
        IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(AttachImagesCommandProperty, value);
    }

    public static IRelayCommand<Exception?>? GetErrorCommand(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(ErrorCommandProperty);
    }

    public static void SetErrorCommand(Control control, IRelayCommand<Exception?>? value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(ErrorCommandProperty, value);
    }

    internal static async Task<bool> TryAttachAsync(
        Control control,
        IReadOnlyList<ImageAttachmentInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(inputs);

        IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? command =
            GetAttachImagesCommand(control);

        if (inputs.Count == 0 || command?.CanExecute(inputs) != true)
        {
            DisposeInputs(inputs);
            return false;
        }

        await command.ExecuteAsync(inputs);

        return true;
    }

    internal static void HandleError(Control control, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(exception);

        IRelayCommand<Exception?>? errorCommand = GetErrorCommand(control);

        if (errorCommand?.CanExecute(exception) == true)
        {
            errorCommand.Execute(exception);
        }
    }

    private static void DisposeInputs(IEnumerable<ImageAttachmentInput> inputs)
    {
        foreach (ImageAttachmentInput input in inputs)
        {
            input.Dispose();
        }
    }
}
