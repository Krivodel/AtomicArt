namespace AtomicArt.Desktop.Services;

public interface IWindowStateService
{
    void Minimize();

    void ToggleWindowState();

    void ShowAndActivate();
}
