using System.Globalization;

using Microsoft.Extensions.Logging;

using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Services.Paths;
using Krivodeling.Localization.Avalonia;

namespace AtomicArt.Desktop.Services.Localization;

public sealed class LocalizationService : ILocalizationService, ILocalizationTextProvider, IDisposable
{
    public IReadOnlyList<LocalizationOption> AvailableLocalizations => _localization.AvailableLocalizations;
    public LocalizationOption? CurrentLocalization => _localization.CurrentLocalization;
    public CultureInfo CurrentCulture => _localization.CurrentCulture;

    public event EventHandler? Changed;

    private readonly Krivodeling.Localization.Avalonia.LocalizationService _localization;
    private readonly IMessenger _messenger;

    public LocalizationService(
        IAtomicArtDataPathProvider pathProvider,
        TrustedFileStreamFactory trustedFileStreamFactory,
        ILogger<Krivodeling.Localization.Avalonia.LocalizationService> logger,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        AtomicArtLocalizationFileStore store = new(pathProvider, trustedFileStreamFactory);
        BuiltInLocalizationCatalog catalog = BuiltInLocalizationCatalog.FromAssemblies(
            typeof(LocalizationService).Assembly,
            typeof(Pica.Viewer.DependencyInjection).Assembly);
        _localization = new Krivodeling.Localization.Avalonia.LocalizationService(store, catalog, logger);
        _localization.Changed += OnLocalizationChanged;
    }

    public Task RefreshAvailableLocalizationsAsync(CancellationToken ct)
    {
        return _localization.RefreshAvailableLocalizationsAsync(ct);
    }

    public void ReconcileCurrentOrSystemDefault()
    {
        _localization.ReconcileCurrentOrSystemDefault();
    }

    public void Select(string localizationId)
    {
        _localization.Select(localizationId);
    }

    public void SelectSavedOrEnglishFallback(string localizationId)
    {
        _localization.SelectSavedOrEnglishFallback(localizationId);
    }

    public string Get(string key)
    {
        return _localization.Get(key);
    }

    public string Format(string key, params object?[] arguments)
    {
        return _localization.Format(key, arguments);
    }

    public void Dispose()
    {
        _localization.Changed -= OnLocalizationChanged;
        _localization.Dispose();
    }

    private void OnLocalizationChanged(object? sender, EventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
        _messenger.Send(new LocalizationChangedMessage());
    }
}
