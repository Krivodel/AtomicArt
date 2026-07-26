namespace AtomicArt.Desktop.Services;

public interface IWindowStateService
{
    void Hide();

    void Minimize();

    void ToggleWindowState();

    void ShowAndActivate();
}
