using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed class GenerationOptionViewModel : ObservableObject
{
    public string Value { get; }
    public string? LocalizationKey { get; }
    public string DisplayName => LocalizationKey is null
        ? Value
        : _textProvider.Get(LocalizationKey);

    private readonly ILocalizationTextProvider _textProvider;

    public GenerationOptionViewModel(
        string value,
        string? localizationKey,
        ILocalizationTextProvider textProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value;
        LocalizationKey = localizationKey;
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
