using System;
using System.IO;
using System.Reflection;

namespace AtomicArt.Installer;

internal sealed class TemporarySetupFile : IDisposable
{
    public string Path { get; }

    private const string ResourceName = "AtomicArtVelopackSetup.exe";

    private TemporarySetupFile(string path)
    {
        Path = path;
    }

    public static TemporarySetupFile Create()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"AtomicArt-Velopack-{Guid.NewGuid():N}.exe");
        Assembly assembly = typeof(TemporarySetupFile).Assembly;
        Stream? resourceStream = assembly.GetManifestResourceStream(
            ResourceName);

        if (resourceStream is null)
        {
            throw new InvalidOperationException(
                "Embedded Velopack setup resource was not found.");
        }

        try
        {
            using FileStream outputStream = new(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            resourceStream.CopyTo(outputStream);
            outputStream.Flush(true);
        }
        catch (IOException)
        {
            DeleteIfExists(path);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            DeleteIfExists(path);
            throw;
        }
        finally
        {
            resourceStream.Dispose();
        }

        return new TemporarySetupFile(path);
    }

    public void Dispose()
    {
        DeleteIfExists(Path);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
