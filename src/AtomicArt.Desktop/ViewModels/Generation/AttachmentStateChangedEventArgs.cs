namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed class AttachmentStateChangedEventArgs : EventArgs
{
    public AttachmentStateChangeKind Kind { get; }
    public Exception? Exception { get; }

    public AttachmentStateChangedEventArgs(
        AttachmentStateChangeKind kind,
        Exception? exception = null)
    {
        Kind = kind;
        Exception = exception;
    }
}
