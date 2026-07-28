using Microsoft.Extensions.Options;

namespace AtomicArt.Desktop.Services.State;

public sealed class StateWritePolicy
{
    public TimeSpan DeferredWriteDelay { get; }

    public StateWritePolicy(IOptions<StatePersistenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        DeferredWriteDelay = TimeSpan.FromMilliseconds(
            options.Value.DeferredWriteDelayMilliseconds);
    }
}
