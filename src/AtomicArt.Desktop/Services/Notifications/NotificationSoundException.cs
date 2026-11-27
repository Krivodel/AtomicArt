namespace AtomicArt.Desktop.Services.Notifications;

public sealed class NotificationSoundException : IOException
{
    public NotificationSoundException(Exception? innerException = null)
        : base("The notification sound is invalid or exceeds the supported limits.", innerException)
    {
    }
}
