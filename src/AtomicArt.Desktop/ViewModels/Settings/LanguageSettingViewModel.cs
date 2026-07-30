using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class LanguageSettingViewModel : SettingItemViewModel
{
    public ReadOnlyObservableCollection<LanguageOptionViewModel> Options { get; }
    public string SearchPlaceholder => TextProvider.Get(
        SettingsLocalizationKeys.Language.SearchPlaceholder);
    public LanguageOptionViewModel? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetProperty(ref _selectedOption, value))
            {
                ApplyCommand.NotifyCanExecuteChanged();

                if (!_isSynchronizingSelection)
                {
                    ApplyCommand.Execute(null);
                }
            }
        }
    }
    public string SearchText
    {
        get => _searchText;
        set
        {
            string normalizedValue = value ?? string.Empty;

            if (SetProperty(ref _searchText, normalizedValue))
            {
                ApplySearch();
            }
        }
    }

    protected override IRelayCommand OperationCommand => ApplyCommand;

    private readonly LanguageSettingDefinition _definition;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly ObservableCollection<LanguageOptionViewModel> _options;
    private LanguageOptionViewModel? _selectedOption;
    private string _searchText = string.Empty;
    private bool _isSynchronizingSelection;

    public LanguageSettingViewModel(
        LanguageSettingDefinition definition,
        ILocalizationService localizationService,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, errorHandler, textProvider)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _localizationService = localizationService
            ?? throw new ArgumentNullException(nameof(localizationService));
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _options = new ObservableCollection<LanguageOptionViewModel>();
        Options = new ReadOnlyObservableCollection<LanguageOptionViewModel>(_options);
        ReplaceOptions();
    }

    public override void RefreshLocalization()
    {
        base.RefreshLocalization();
        OnPropertyChanged(nameof(SearchPlaceholder));
        ReplaceOptions();
    }

    protected override void NotifyOperationCanExecuteChanged()
    {
        base.NotifyOperationCanExecuteChanged();
        RefreshOptionsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync(CancellationToken ct)
    {
        if (SelectedOption is not LanguageOptionViewModel selectedOption)
        {
            return;
        }

        await RunOperationAsync(
            async () =>
            {
                _localizationService.Select(selectedOption.Localization.Id);
                await _settingsStateService.SaveValueAsync(
                    _definition,
                    selectedOption.Localization.Id,
                    ct);
            },
            ct,
            nameof(ApplyAsync));
    }

    private bool CanApply()
    {
        return SelectedOption is not null && !IsLoading;
    }

    [RelayCommand(CanExecute = nameof(CanRefreshOptions))]
    private async Task RefreshOptionsAsync(CancellationToken ct)
    {
        await RunOperationAsync(
            async () =>
            {
                await _localizationService.RefreshAvailableLocalizationsAsync(ct);
                _localizationService.ReconcileCurrentOrSystemDefault();
                ReplaceOptions();
            },
            ct,
            nameof(RefreshOptionsAsync));
    }

    private bool CanRefreshOptions()
    {
        return !IsLoading;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    private void ReplaceOptions()
    {
        IReadOnlyList<LocalizationOption> available =
            _localizationService.AvailableLocalizations;

        if (!OptionsMatch(available))
        {
            SynchronizeSelectedOption(null);
            _options.Clear();

            foreach (LocalizationOption option in available)
            {
                _options.Add(new LanguageOptionViewModel(option));
            }
        }

        ApplySearch();
        SynchronizeSelectedOption(FindCurrentOption());
    }

    private bool OptionsMatch(IReadOnlyList<LocalizationOption> available)
    {
        if (_options.Count != available.Count)
        {
            return false;
        }

        for (int index = 0; index < available.Count; index++)
        {
            if (_options[index].Localization != available[index])
            {
                return false;
            }
        }

        return true;
    }

    private LanguageOptionViewModel? FindCurrentOption()
    {
        LocalizationOption? current = _localizationService.CurrentLocalization;

        if (current is null)
        {
            return null;
        }

        return _options.FirstOrDefault(option => string.Equals(
            option.Localization.Id,
            current.Id,
            StringComparison.OrdinalIgnoreCase));
    }

    private void ApplySearch()
    {
        foreach (LanguageOptionViewModel option in _options)
        {
            option.ApplySearch(SearchText);
        }
    }

    private void SynchronizeSelectedOption(LanguageOptionViewModel? selectedOption)
    {
        _isSynchronizingSelection = true;

        try
        {
            SelectedOption = selectedOption;
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }
}
