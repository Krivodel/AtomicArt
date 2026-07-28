using Microsoft.Extensions.Options;

using AtomicArt.Desktop.Services.Concurrency;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationConcurrencyLimiter : SemaphoreConcurrencyLimiter, IGenerationConcurrencyLimiter
{
    public GenerationConcurrencyLimiter(
        IOptions<GenerationClientOptions> options)
        : base(GetMaximumConcurrency(options))
    {
    }

    private static int GetMaximumConcurrency(
        IOptions<GenerationClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Value.MaxConcurrentGenerations;
    }
}
