namespace AtomicArt.Desktop.Services.Notifications;

public interface IGenerationNotificationPresenter
{
    Task ShowAsync(GenerationNotification notification, CancellationToken ct);
}
