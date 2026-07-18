namespace AtomicArt.Desktop.Services;

public interface ITrayService
{
    bool IsExitRequested { get; }

    void HideToTray();

    void ShowWindow();

    void ExitApplication();
}
