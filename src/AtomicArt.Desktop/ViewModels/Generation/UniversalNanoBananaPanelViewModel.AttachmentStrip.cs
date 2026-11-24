using System.Windows.Input;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class UniversalNanoBananaPanelViewModel : IGenerationAttachmentStrip
{
    bool IGenerationAttachmentStrip.ShowsAttachmentActions => true;
    bool IGenerationAttachmentStrip.ShowsAttachmentStrip => true;
    ICommand IGenerationAttachmentStrip.PickImage => PickImageCommand;
    ICommand IGenerationAttachmentStrip.OpenAttachment => OpenAttachmentCommand;
    ICommand IGenerationAttachmentStrip.RemoveAttachment => RemoveAttachmentCommand;
    ICommand IGenerationAttachmentStrip.ReorderAttachment => ReorderAttachmentCommand;
}
