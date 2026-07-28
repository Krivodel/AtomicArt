using Microsoft.Extensions.Options;

using AtomicArt.Desktop.Services.Concurrency;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class AttachedImagePreparationConcurrencyLimiter : SemaphoreConcurrencyLimiter
{
    public AttachedImagePreparationConcurrencyLimiter(
        IOptions<GenerationClientOptions> options)
        : base(GetMaximumConcurrency(options))
    {
    }

    private static int GetMaximumConcurrency(
        IOptions<GenerationClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Value.AttachedImagePreparationConcurrency == 0
            ? Environment.ProcessorCount
            : options.Value.AttachedImagePreparationConcurrency;
    }
}
