namespace AtomicArt.Desktop.Services.Notifications;

public interface INotificationSoundStore
{
    bool HasCustomSound { get; }

    Task ImportAsync(string sourcePath, CancellationToken ct);
    Task ResetAsync(CancellationToken ct);
    Task<byte[]?> ReadAsync(CancellationToken ct);
}
