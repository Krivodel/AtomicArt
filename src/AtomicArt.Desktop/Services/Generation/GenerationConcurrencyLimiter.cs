using AtomicArt.Desktop.Services.Concurrency;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationConcurrencyLimiter : SemaphoreConcurrencyLimiter, IGenerationConcurrencyLimiter
{
    public const int MaxConcurrentGenerations = 64;

    public GenerationConcurrencyLimiter()
        : base(MaxConcurrentGenerations)
    {
    }
}
