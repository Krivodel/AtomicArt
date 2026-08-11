namespace AtomicArt.Desktop.Services;

public sealed record LocalizedConfirmationDialogRequest(
    string TitleLocalizationKey,
    string MessageLocalizationKey,
    string ConfirmActionLocalizationKey,
    IReadOnlyList<object?> MessageArguments);
