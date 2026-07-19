using AtomicArt.Desktop.Services;

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
                NotifyActionCanExecuteChanged();
            }
        }
    }

    private TOption? _selectedOption;

    protected SelectableSettingItemViewModel(
        IDisplaySettingDefinition definition,
        IReadOnlyList<TOption> options,
        TOption? selectedOption,
        IViewModelErrorHandler errorHandler)
        : base(definition, errorHandler)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
        _selectedOption = selectedOption;
    }

    protected bool HasSelectedOption => SelectedOption is not null;
}
