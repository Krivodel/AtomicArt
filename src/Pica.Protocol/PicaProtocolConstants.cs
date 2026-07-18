namespace Pica.Protocol;

public static class PicaProtocolConstants
{
    public const string ApplicationName = "Pica";
    public const int CurrentVersion = 1;
    public const string PipeArgument = "--pica-pipe";
    public const int MaximumMessageBytes = 4 * 1024 * 1024;

    public static string ExecutableName => OperatingSystem.IsWindows()
        ? $"{ApplicationName}.exe"
        : ApplicationName;
}
