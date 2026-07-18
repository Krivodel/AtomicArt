using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia;

using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class DialogService : IDialogService, IDialogWindowAttachmentService
{
    private const double DialogWidth = 420;
    private const double DialogMinHeight = 160;
    private const double DialogPadding = 20;
    private const double DialogSpacing = 16;
    private const double CloseButtonWidth = 120;

    private Window? _owner;

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _owner = window;
    }

    public bool ShowError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (_owner is null)
        {
            return false;
        }

        Window dialog = CreateErrorDialog(message);
        dialog.Show(_owner);

        return true;
    }

    public Task<bool> ShowErrorAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        bool isShown = ShowError(message);

        return Task.FromResult(isShown);
    }

    private static Window CreateErrorDialog(string message)
    {
        Button closeButton = new()
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = CloseButtonWidth
        };

        Window dialog = new()
        {
            Title = UiStrings.UnhandledExceptionTitle,
            Width = DialogWidth,
            MinHeight = DialogMinHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(DialogPadding),
                Spacing = DialogSpacing,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    closeButton
                }
            }
        };

        closeButton.Click += (_, _) => dialog.Close();

        return dialog;
    }
}
