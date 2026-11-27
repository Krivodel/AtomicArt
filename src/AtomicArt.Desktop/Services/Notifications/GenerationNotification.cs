namespace AtomicArt.Desktop.Services.Notifications;

public sealed record GenerationNotification(
    string Prompt,
    bool IsError,
    string? FailureCode = null)
{
    public static TimeSpan DisplayDuration { get; } = TimeSpan.FromSeconds(5);
}
