using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal static class GenerationRunDispatcherTestFactory
{
    public static GenerationRunDispatcher Create(
        IImageGenerationApiClient apiClient,
        IGenerationLifecycleEventHub lifecycleEventHub,
        IGenerationConcurrencyLimiter? limiter = null,
        int maxAutomaticRetries = TestApiConfiguration.MaxAutomaticRetries,
        ILogger<GenerationRunDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(lifecycleEventHub);

        return new GenerationRunDispatcher(
            limiter ?? new GenerationConcurrencyLimiter(
                Options.Create(
                    TestApiConfiguration.CreateGenerationOptions())),
            apiClient,
            new NanoBanana2GenerationLifecyclePublisher(lifecycleEventHub),
            new NullGenerationResultStorage(),
            TestGenerationActivityTrackerFactory.Create(),
            new GenerationAdmissionGate(),
            logger ?? NullLogger<GenerationRunDispatcher>.Instance,
            Options.Create(
                TestApiConfiguration.CreateGenerationOptions(
                    maxAutomaticRetries: maxAutomaticRetries)));
    }
}
