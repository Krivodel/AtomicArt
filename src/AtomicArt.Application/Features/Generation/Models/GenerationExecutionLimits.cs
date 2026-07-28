namespace AtomicArt.Application.Features.Generation.Models;

public sealed class GenerationExecutionLimits
{
    public long EmergencyMaxProviderResponseBytes { get; }

    public GenerationExecutionLimits(
        long emergencyMaxProviderResponseBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            emergencyMaxProviderResponseBytes,
            1L);

        EmergencyMaxProviderResponseBytes =
            emergencyMaxProviderResponseBytes;
    }
}
