using CommunityToolkit.Mvvm.ComponentModel;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class GenerationModelVisibilityOptionViewModel : ObservableObject
{
    public string ModelId { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    private bool _isVisible;

    public GenerationModelVisibilityOptionViewModel(
        string modelId,
        string displayName,
        bool isVisible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ModelId = modelId;
        DisplayName = displayName;
        _isVisible = isVisible;
    }
}
