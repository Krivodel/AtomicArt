namespace AtomicArt.Desktop;

[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class DesktopConfigurationFileAttribute : Attribute
{
    public string FileName { get; }

    public DesktopConfigurationFileAttribute(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        FileName = fileName;
    }
}
