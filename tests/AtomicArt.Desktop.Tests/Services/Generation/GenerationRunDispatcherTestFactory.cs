using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal static class GenerationRunDispatcherTestFactory
{
    public static GenerationRunDispatcher Create(
        IImageGenerationApiClient apiClient,
        IGenerationLifecycleEventHub lifecycleEventHub,
        IGenerationConcurrencyLimiter? limiter = null)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(lifecycleEventHub);

        return new GenerationRunDispatcher(
            limiter ?? new GenerationConcurrencyLimiter(),
            apiClient,
            new NanoBanana2GenerationLifecyclePublisher(lifecycleEventHub),
            new NullGenerationResultStorage(),
            TestGenerationActivityTrackerFactory.Create(),
            NullLogger<GenerationRunDispatcher>.Instance);
    }
}
