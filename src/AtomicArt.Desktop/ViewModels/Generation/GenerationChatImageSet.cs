using CommunityToolkit.Mvvm.ComponentModel;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatImageSet : ObservableObject
{
    public IReadOnlyList<GenerationChatImageViewModel> Images { get; }
    public bool IsSingleImage => Images.Count == 1;

    [ObservableProperty]
    private bool _isCurrent = true;

    public GenerationChatImageSet(IReadOnlyList<GenerationChatImageViewModel> images)
    {
        Images = images ?? throw new ArgumentNullException(nameof(images));
    }
}
