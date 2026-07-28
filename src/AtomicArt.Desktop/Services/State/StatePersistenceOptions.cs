namespace AtomicArt.Desktop.Services.State;

public sealed class StatePersistenceOptions
{
    public const string SectionName = "State";

    public int DeferredWriteDelayMilliseconds { get; init; }

    public static bool IsValid(StatePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.DeferredWriteDelayMilliseconds > 0;
    }
}
