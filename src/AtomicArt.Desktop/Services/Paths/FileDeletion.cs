namespace AtomicArt.Desktop.Services.Paths;

internal static class FileDeletion
{
    internal static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
