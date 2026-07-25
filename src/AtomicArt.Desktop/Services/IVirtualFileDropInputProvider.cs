namespace AtomicArt.Desktop.Services;

internal interface IVirtualFileDropInputProvider
{
    bool TryTakeInputs(out IReadOnlyList<ImageAttachmentInput> inputs);
}
