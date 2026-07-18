using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Generation;

public interface IModelPanelViewModel
{
    string PanelId { get; }
    string ModelId { get; }
    string DisplayName { get; }
    int MaxAttachedImageBytes { get; }
    int AttachmentInputByteLimit { get; }
    IAsyncRelayCommand GenerateCommand { get; }
    IAsyncRelayCommand PickImageCommand { get; }
    IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> AttachImagesCommand { get; }
    IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?> AttachImageInputsCommand { get; }

    bool SupportsModel(string modelId);
}
