namespace AtomicArt.Desktop.Services.Gallery;

public interface IGallerySaveNotificationService
{
    void ShowSaving(string fileName);

    void Dismiss();
}
