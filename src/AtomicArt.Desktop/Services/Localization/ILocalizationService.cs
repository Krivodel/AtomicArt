using System.Globalization;

namespace AtomicArt.Desktop.Services.Localization;

public interface ILocalizationService
{
    IReadOnlyList<LocalizationOption> AvailableLocalizations { get; }
    LocalizationOption? CurrentLocalization { get; }
    CultureInfo CurrentCulture { get; }

    Task RefreshAvailableLocalizationsAsync(CancellationToken ct);

    void ReconcileCurrentOrSystemDefault();

    void Select(string localizationId);

    void SelectSavedOrEnglishFallback(string localizationId);
}
