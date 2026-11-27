namespace AtomicArt.Desktop.Services.Notifications;

public interface INotificationSoundPlayer
{
    void Play(CancellationToken ct);
}
