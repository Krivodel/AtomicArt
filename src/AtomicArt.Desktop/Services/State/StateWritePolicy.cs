namespace AtomicArt.Desktop.Services.State;

internal static class StateWritePolicy
{
    public static readonly TimeSpan DeferredWriteDelay = TimeSpan.FromMilliseconds(350);
}
