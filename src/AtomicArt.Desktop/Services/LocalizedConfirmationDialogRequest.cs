namespace AtomicArt.Desktop.Services;

public sealed record LocalizedConfirmationDialogRequest(
    string TitleLocalizationKey,
    string MessageLocalizationKey,
    string ConfirmActionLocalizationKey,
    string CancelActionLocalizationKey,
    ConfirmationDialogKind Kind,
    ConfirmationDialogBackgroundClickBehavior BackgroundClickBehavior,
    IReadOnlyList<object?> MessageArguments);
