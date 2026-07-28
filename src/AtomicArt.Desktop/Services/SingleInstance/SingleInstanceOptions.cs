namespace AtomicArt.Desktop.Services.SingleInstance;

internal sealed class SingleInstanceOptions
{
    public const string SectionName = "SingleInstance";

    public int ClientProtocolTimeoutMilliseconds { get; init; }
    public int ListenerRetryDelayMilliseconds { get; init; }
    public int PipeConnectAttemptCount { get; init; }
    public int PipeConnectRetryDelayMilliseconds { get; init; }
    public int PipeConnectTimeoutMilliseconds { get; init; }

    public static bool IsValid(SingleInstanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.ClientProtocolTimeoutMilliseconds > 0
            && options.ListenerRetryDelayMilliseconds > 0
            && options.PipeConnectAttemptCount > 0
            && options.PipeConnectRetryDelayMilliseconds > 0
            && options.PipeConnectTimeoutMilliseconds > 0;
    }
}
