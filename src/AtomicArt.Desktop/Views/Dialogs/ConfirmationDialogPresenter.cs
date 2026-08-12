using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Enums;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Views.Dialogs;

public sealed class ConfirmationDialogPresenter : IConfirmationDialogPresenter
{
    public bool IsOpen => _activeDialog is not null;

    private readonly ISukiDialogManager _manager;
    private readonly IUiScaleService _uiScaleService;
    private ISukiDialog? _activeDialog;
    private ScaleTransform? _activeDialogScale;
    private TextBlock? _messageText;
    private Button? _confirmButton;
    private Button? _cancelButton;

    public ConfirmationDialogPresenter(
        ISukiDialogManager manager,
        IUiScaleService uiScaleService)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _uiScaleService = uiScaleService
            ?? throw new ArgumentNullException(nameof(uiScaleService));
    }

    public async Task<bool> ShowAsync(
        ConfirmationDialogPresentation presentation,
        CancellationToken ct)
    {
        ValidatePresentation(presentation);
        ct.ThrowIfCancellationRequested();

        if (_activeDialog is not null)
        {
            throw new InvalidOperationException(
                "A confirmation dialog is already open.");
        }

        TextBlock messageText = new()
        {
            Text = presentation.Message,
            TextWrapping = TextWrapping.Wrap
        };
        ScaleTransform dialogScale = new();
        ApplyScale(dialogScale, _uiScaleService.CurrentScale);
        SukiDialog dialog = new()
        {
            Manager = _manager,
            Title = presentation.Title,
            Content = messageText,
            RenderTransform = dialogScale,
            RenderTransformOrigin = RelativePoint.Center,
            CanDismissWithBackgroundClick = GetCanDismissWithBackgroundClick(
                presentation.BackgroundClickBehavior)
        };
        dialog.Classes.Add("confirmation-dialog");
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        (Button confirmButton, Button cancelButton) = AddActionButtons(
            dialog,
            completion,
            presentation);
        dialog.OnDismissed = _ => completion.TrySetResult(false);

        if (!_manager.TryShowDialog(dialog))
        {
            throw new InvalidOperationException(
                "Opening the confirmation dialog failed because another SukiUI dialog is already open.");
        }

        _activeDialog = dialog;
        _activeDialogScale = dialogScale;
        _messageText = messageText;
        _confirmButton = confirmButton;
        _cancelButton = cancelButton;
        _uiScaleService.ScaleChanged += OnUiScaleChanged;

        try
        {
            return await completion.Task.WaitAsync(ct);
        }
        finally
        {
            _uiScaleService.ScaleChanged -= OnUiScaleChanged;
            _manager.TryDismissDialog(dialog);
            _activeDialog = null;
            _activeDialogScale = null;
            _messageText = null;
            _confirmButton = null;
            _cancelButton = null;
        }
    }

    public void Update(ConfirmationDialogPresentation presentation)
    {
        ValidatePresentation(presentation);

        if (_activeDialog is null
            || _messageText is null
            || _confirmButton is null
            || _cancelButton is null)
        {
            return;
        }

        _activeDialog.Title = presentation.Title;
        _messageText.Text = presentation.Message;
        _confirmButton.Content = presentation.ConfirmActionText;
        _cancelButton.Content = presentation.CancelActionText;
        _activeDialog.CanDismissWithBackgroundClick =
            GetCanDismissWithBackgroundClick(
                presentation.BackgroundClickBehavior);
    }

    public void Dismiss()
    {
        if (_activeDialog is not null)
        {
            _manager.TryDismissDialog(_activeDialog);
        }
    }

    private static (Button ConfirmButton, Button CancelButton) AddActionButtons(
        ISukiDialog dialog,
        TaskCompletionSource<bool> completion,
        ConfirmationDialogPresentation presentation)
    {
        Button confirmButton;
        Button cancelButton;

        if (presentation.Kind == ConfirmationDialogKind.Destructive)
        {
            cancelButton = AddActionButton(
                dialog,
                presentation.CancelActionText,
                completion,
                false,
                SukiButtonStyles.Basic);
            confirmButton = AddActionButton(
                dialog,
                presentation.ConfirmActionText,
                completion,
                true,
                SukiButtonStyles.Danger);
        }
        else
        {
            confirmButton = AddActionButton(
                dialog,
                presentation.ConfirmActionText,
                completion,
                true,
                SukiButtonStyles.Accent);
            cancelButton = AddActionButton(
                dialog,
                presentation.CancelActionText,
                completion,
                false,
                SukiButtonStyles.Basic);
        }

        return (confirmButton, cancelButton);
    }

    private static Button AddActionButton(
        ISukiDialog dialog,
        string content,
        TaskCompletionSource<bool> completion,
        bool result,
        SukiButtonStyles style)
    {
        Button button = new()
        {
            Content = content
        };
        button.Classes.Add(style.ToString());
        button.Click += (_, _) =>
        {
            completion.TrySetResult(result);
            dialog.Dismiss();
        };
        dialog.ActionButtons.Add(button);

        return button;
    }

    private static void ValidatePresentation(
        ConfirmationDialogPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentException.ThrowIfNullOrWhiteSpace(presentation.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(presentation.Message);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            presentation.ConfirmActionText);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            presentation.CancelActionText);
    }

    private static bool GetCanDismissWithBackgroundClick(
        ConfirmationDialogBackgroundClickBehavior behavior)
    {
        return behavior switch
        {
            ConfirmationDialogBackgroundClickBehavior.Dismiss => true,
            ConfirmationDialogBackgroundClickBehavior.Ignore => false,
            _ => throw new ArgumentOutOfRangeException(
                nameof(behavior),
                behavior,
                "Unknown confirmation dialog background click behavior.")
        };
    }

    private static void ApplyScale(ScaleTransform transform, double scale)
    {
        transform.ScaleX = scale;
        transform.ScaleY = scale;
    }

    private void OnUiScaleChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeDialogScale is null)
        {
            return;
        }

        ApplyScale(_activeDialogScale, _uiScaleService.CurrentScale);
    }
}
