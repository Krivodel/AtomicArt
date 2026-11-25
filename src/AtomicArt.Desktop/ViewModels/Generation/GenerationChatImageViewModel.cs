using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatImageViewModel(Guid id, bool isPending = false) : ObservableObject
{
    public Guid Id { get; } = id;
    public GenerationReference? Reference => _reference;

    private GenerationReference? _reference;
    [ObservableProperty]
    private string? _textReply;
    [ObservableProperty]
    private bool _isFailed;
    [ObservableProperty]
    private bool _isPending = isPending;
    [ObservableProperty]
    private bool _canChoose;
    [ObservableProperty]
    private double? _previewAspectRatio;

    internal void Complete(GenerationReference? reference, string? textReply = null)
    {
        if (!object.Equals(_reference, reference))
        {
            PreviewAspectRatio = null;
        }

        SetProperty(ref _reference, reference, nameof(Reference));
        TextReply = textReply;
        IsFailed = reference is null && textReply is null;
        IsPending = false;
    }
}
