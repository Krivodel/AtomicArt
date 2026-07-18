namespace AtomicArt.Desktop.Services;

public interface IViewModelErrorHandler
{
    void Log(Exception exception, string operationName);

    string GetUserMessage(Exception exception);
}
