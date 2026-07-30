using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class LanguageOptionViewModel : ObservableObject
{
    public LocalizationOption Localization { get; }
    public string DisplayName => Localization.Id;
    public bool IsSearchMatch
    {
        get => _isSearchMatch;
        private set => SetProperty(ref _isSearchMatch, value);
    }

    private bool _isSearchMatch = true;

    public LanguageOptionViewModel(LocalizationOption localization)
    {
        Localization = localization
            ?? throw new ArgumentNullException(nameof(localization));
    }

    public void ApplySearch(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            IsSearchMatch = true;
            return;
        }

        string normalizedSearchText = searchText.Trim();
        IsSearchMatch = DisplayName.Contains(
            normalizedSearchText,
            StringComparison.CurrentCultureIgnoreCase);
    }
}
