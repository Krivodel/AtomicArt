namespace AtomicArt.Desktop.Services.Paths;

internal static class AtomicBootstrapFileWriter
{
    internal static async Task WriteAsync(
        string directory,
        string targetPath,
        string temporaryFilePrefix,
        ReadOnlyMemory<byte> content,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryFilePrefix);

        Directory.CreateDirectory(directory);
        string temporaryPath = AtomicFileWriteTempPath.CreateHidden(
            directory,
            temporaryFilePrefix);

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            ReplaceFile(temporaryPath, targetPath);
        }
        finally
        {
            FileDeletion.DeleteIfExists(temporaryPath);
        }
    }

    private static void ReplaceFile(string temporaryPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(
                temporaryPath,
                targetPath,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);
            return;
        }

        File.Move(temporaryPath, targetPath);
    }
}
