namespace AtomicArt.Contracts.Generation;

public static class GenerationAttemptLimits
{
    public const int MinimumAttemptNumber = 1;
    public const int MaximumAttemptNumber = 5;
    public const int MaximumAutomaticRetries = MaximumAttemptNumber - 1;
}
