using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class GpuResourceCacheOptionViewModel : ObservableObject
{
    public string DisplayName => Definition.Megabytes is int megabytes
        ? _textProvider.Format(Definition.DisplayNameKey, megabytes)
        : _textProvider.Get(Definition.DisplayNameKey);
    public string Value => Definition.Value;
    public int? Megabytes => Definition.Megabytes;

    internal GpuResourceCacheOptionDefinition Definition { get; }

    private readonly ILocalizationTextProvider _textProvider;

    public GpuResourceCacheOptionViewModel(
        GpuResourceCacheOptionDefinition definition,
        ILocalizationTextProvider textProvider)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
