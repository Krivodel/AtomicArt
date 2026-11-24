using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed class AttachedImageViewModel : ObservableObject
{
    public Guid Id { get; }
    public string FileName => _fileName;
    public string ContentType => _contentType;
    public PanelAttachmentState? State => _state;
    public GenerationReference? SavedReference { get; }
    public bool IsLoading => _isLoading;
    public bool IsReady => !_isLoading && _content is not null;
    public byte[] Content => _content is null
        ? []
        : (byte[])_content.Clone();

    private byte[]? _content;
    private string _fileName;
    private string _contentType;
    private PanelAttachmentState? _state;
    private bool _isLoading;

    public AttachedImageViewModel(
        AttachedImageDto attachedImage,
        PanelAttachmentState? state = null)
    {
        ArgumentNullException.ThrowIfNull(attachedImage);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachedImage.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachedImage.ContentType);
        ArgumentNullException.ThrowIfNull(attachedImage.Content);

        Id = Guid.NewGuid();
        _fileName = attachedImage.FileName;
        _contentType = attachedImage.ContentType;
        _state = state;
        _content = (byte[])attachedImage.Content.Clone();
    }

    internal AttachedImageViewModel(GenerationReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        Id = Guid.NewGuid();
        _fileName = reference.FileName;
        _contentType = reference.ContentType;
        SavedReference = reference;
    }

    private AttachedImageViewModel(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        Id = Guid.NewGuid();
        _fileName = fileName;
        _contentType = string.Empty;
        _isLoading = true;
    }

    public static AttachedImageViewModel CreateLoading(string fileName)
    {
        return new AttachedImageViewModel(fileName);
    }

    public void Complete(AttachedImageDto attachedImage, PanelAttachmentState state)
    {
        ArgumentNullException.ThrowIfNull(attachedImage);
        ArgumentNullException.ThrowIfNull(state);

        _content = (byte[])attachedImage.Content.Clone();
        _fileName = state.FileName;
        _contentType = attachedImage.ContentType;
        _state = state;
        _isLoading = false;
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(ContentType));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsReady));
    }

    public AttachedImageDto ToDto()
    {
        if (!IsReady || _content is null)
        {
            throw new InvalidOperationException("Loading attachment cannot be converted to a DTO.");
        }

        return new AttachedImageDto(
            FileName,
            ContentType,
            (byte[])_content.Clone());
    }

    internal PanelAttachmentState GetRequiredState()
    {
        return _state ?? throw new InvalidOperationException("Ready attachment has no persisted state.");
    }
}
