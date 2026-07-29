using System.Globalization;

using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class TestLocalizationService :
    ILocalizationService,
    ILocalizationTextProvider
{
    public IReadOnlyList<LocalizationOption> AvailableLocalizations { get; set; } =
        new List<LocalizationOption>();
    public LocalizationOption? CurrentLocalization { get; set; }
    public CultureInfo CurrentCulture => CultureInfo.CurrentCulture;
    public Action? RefreshAction { get; set; }

    private readonly List<string>? _calls;
    private readonly TestLocalizationTextProvider _textProvider = new();

    public TestLocalizationService(List<string>? calls = null)
    {
        _calls = calls;
    }

    public Task RefreshAvailableLocalizationsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _calls?.Add("localization.refresh");
        RefreshAction?.Invoke();

        return Task.CompletedTask;
    }

    public void ReconcileCurrentOrSystemDefault()
    {
        _calls?.Add("localization.reconcile");
    }

    public void Select(string localizationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationId);

        CurrentLocalization = AvailableLocalizations.Single(option =>
            string.Equals(
                option.Id,
                localizationId,
                StringComparison.OrdinalIgnoreCase));
    }

    public void SelectSavedOrEnglishFallback(string localizationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationId);

        CurrentLocalization = AvailableLocalizations.FirstOrDefault(option =>
            string.Equals(
                option.Id,
                localizationId,
                StringComparison.OrdinalIgnoreCase));
    }

    public string Get(string key)
    {
        return _textProvider.Get(key);
    }

    public string Format(string key, params object?[] arguments)
    {
        return _textProvider.Format(key, arguments);
    }
}
