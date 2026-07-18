using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal static class TestGenerationActivityTrackerFactory
{
    public static IGenerationActivityTracker Create()
    {
        return new GenerationActivityTracker(
            NullLogger<GenerationActivityTracker>.Instance);
    }
}
