namespace AtomicArt.Api.Generation;

public interface IGenerationRequestConcurrencyLimiter
{
    IDisposable? TryAcquire();
}
