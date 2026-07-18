using AtomicArt.Desktop.Services.Concurrency;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class AttachedImagePreparationConcurrencyLimiter : SemaphoreConcurrencyLimiter
{
    public static int MaximumConcurrency => Environment.ProcessorCount;

    public AttachedImagePreparationConcurrencyLimiter()
        : base(MaximumConcurrency)
    {
    }
}
