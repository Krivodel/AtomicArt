using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AtomicArt.Desktop.ViewModels.Generation;

public interface IGenerationAttachmentStrip
{
    ReadOnlyObservableCollection<AttachedImageViewModel> AttachedImages { get; }
    string AttachmentCounterText { get; }
    string? PanelId { get; }
    bool ShowsAttachmentActions { get; }
    bool ShowsAttachmentStrip { get; }
    ICommand PickImage { get; }
    ICommand OpenAttachment { get; }
    ICommand? RemoveAttachment { get; }
    ICommand? ReorderAttachment { get; }
}
