namespace AtomicArt.Desktop.Services;

public sealed record ConfirmationDialogPresentation(
    string Title,
    string Message,
    string ConfirmActionText,
    string CancelActionText,
    ConfirmationDialogKind Kind,
    ConfirmationDialogBackgroundClickBehavior BackgroundClickBehavior);
