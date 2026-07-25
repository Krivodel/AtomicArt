using Avalonia.Input;

namespace AtomicArt.Desktop.Services;

internal static class VirtualFileDataTransferFormats
{
    public const string AnsiDescriptor = "FileGroupDescriptor";
    public const string Contents = "FileContents";
    public const string UnicodeDescriptor = "FileGroupDescriptorW";

    public static bool ContainsVirtualFiles(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return dataTransfer.Formats.Any(format =>
            string.Equals(
                format.Identifier,
                UnicodeDescriptor,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                format.Identifier,
                AnsiDescriptor,
                StringComparison.OrdinalIgnoreCase));
    }
}
