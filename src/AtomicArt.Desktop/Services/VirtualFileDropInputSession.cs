namespace AtomicArt.Desktop.Services;

internal sealed class VirtualFileDropInputSession : IVirtualFileDropInputProvider
{
    private IReadOnlyList<ImageAttachmentInput>? _inputs;

    public IDisposable Begin(IReadOnlyList<ImageAttachmentInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (_inputs is not null)
        {
            throw new InvalidOperationException(
                "A virtual file drop session is already active.");
        }

        _inputs = inputs;

        return new Scope(this);
    }

    public bool TryTakeInputs(out IReadOnlyList<ImageAttachmentInput> inputs)
    {
        if (_inputs is null)
        {
            inputs = Array.Empty<ImageAttachmentInput>();
            return false;
        }

        inputs = _inputs;
        _inputs = null;
        return true;
    }

    private void End()
    {
        IReadOnlyList<ImageAttachmentInput>? inputs = _inputs;
        _inputs = null;

        if (inputs is null)
        {
            return;
        }

        foreach (ImageAttachmentInput input in inputs)
        {
            input.Dispose();
        }
    }

    private sealed class Scope(VirtualFileDropInputSession session) : IDisposable
    {
        private VirtualFileDropInputSession? _session = session;

        public void Dispose()
        {
            VirtualFileDropInputSession? current = Interlocked.Exchange(
                ref _session,
                null);
            current?.End();
        }
    }
}
