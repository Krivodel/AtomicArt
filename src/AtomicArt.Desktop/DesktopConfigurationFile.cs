namespace AtomicArt.Desktop;

internal static class DesktopConfigurationFile
{
    public static string Name { get; } = GetName();

    private static string GetName()
    {
        DesktopConfigurationFileAttribute? attribute = Attribute.GetCustomAttribute(
            typeof(DesktopConfigurationFile).Assembly,
            typeof(DesktopConfigurationFileAttribute)) as DesktopConfigurationFileAttribute;

        return attribute?.FileName
            ?? throw new InvalidOperationException("Desktop configuration file metadata is missing.");
    }
}
