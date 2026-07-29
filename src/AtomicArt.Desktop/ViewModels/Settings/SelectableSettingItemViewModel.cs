using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Settings;

public abstract class SelectableSettingItemViewModel<TOption> : SettingItemViewModel
    where TOption : class
{
    public IReadOnlyList<TOption> Options { get; }

    public TOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetProperty(ref _selectedOption, value))
            {
                NotifyOperationCanExecuteChanged();

                if (!_isSynchronizingSelectedOption)
                {
                    OnSelectedOptionChanged(value);
                }
            }
        }
    }

    private TOption? _selectedOption;
    private bool _isSynchronizingSelectedOption;

    protected SelectableSettingItemViewModel(
        IDisplaySettingDefinition definition,
        IReadOnlyList<TOption> options,
        TOption? selectedOption,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, errorHandler, textProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
        _selectedOption = selectedOption;
    }

    protected bool HasSelectedOption => SelectedOption is not null;

    protected void SynchronizeSelectedOption(TOption? selectedOption)
    {
        _isSynchronizingSelectedOption = true;

        try
        {
            SelectedOption = selectedOption;
        }
        finally
        {
            _isSynchronizingSelectedOption = false;
        }
    }

    protected virtual void OnSelectedOptionChanged(TOption? selectedOption)
    {
    }
}
