using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatImageViewModel(Guid id, bool isPending = false) : ObservableObject
{
    public Guid Id { get; } = id;
    public GenerationReference? Reference => _reference;

    private GenerationReference? _reference;
    [ObservableProperty]
    private bool _isFailed;
    [ObservableProperty]
    private bool _isPending = isPending;
    [ObservableProperty]
    private bool _canChoose;
    [ObservableProperty]
    private double _previewAspectRatio = 1d;

    internal void Complete(GenerationReference? reference)
    {
        SetProperty(ref _reference, reference, nameof(Reference));
        IsFailed = reference is null;
        IsPending = false;
    }
}
