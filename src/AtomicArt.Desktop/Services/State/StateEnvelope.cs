namespace AtomicArt.Desktop.Services.State;

public sealed record StateEnvelope<TPayload>
{
    public required int SchemaVersion { get; init; }
    public required DateTimeOffset SavedAtUtc { get; init; }
    public required TPayload Payload { get; init; }
}
