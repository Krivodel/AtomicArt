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
        IsSearchMatch = Localization.MatchesSearch(searchText);
    }
}
