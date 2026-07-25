namespace AtomicArt.Desktop.Services.Paths;

internal sealed class DataRootMigrationPlanner
{
    public DataRootMigrationPlanner()
    {
    }

    internal DataRootMigrationPlan Create(
        string sourceRootDirectory,
        string destinationRootDirectory,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootDirectory);
        ct.ThrowIfCancellationRequested();

        string sourceRoot = NormalizeRoot(sourceRootDirectory);
        string destinationRoot = NormalizeRoot(destinationRootDirectory);
        ValidateDistinctNonOverlappingRoots(sourceRoot, destinationRoot);
        ValidateDirectory(sourceRoot, allowMissing: true);
        ValidateDirectory(destinationRoot, allowMissing: false);
        ValidateDestinationIsEmpty(destinationRoot);
        ValidateDestinationWritable(destinationRoot);

        List<string> relativeDirectories = [];
        List<DataRootMigrationFile> files = [];
        long totalBytes = 0;

        if (Directory.Exists(sourceRoot))
        {
            EnumerateSource(
                sourceRoot,
                sourceRoot,
                relativeDirectories,
                files,
                ref totalBytes,
                ct);
        }

        ct.ThrowIfCancellationRequested();
        ValidateAvailableSpace(destinationRoot, totalBytes);

        return new DataRootMigrationPlan
        {
            SourceRootDirectory = sourceRoot,
            DestinationRootDirectory = destinationRoot,
            RelativeDirectories = relativeDirectories,
            Files = files,
            TotalBytes = totalBytes
        };
    }

    private static string NormalizeRoot(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static void ValidateDistinctNonOverlappingRoots(
        string sourceRoot,
        string destinationRoot)
    {
        if (string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected data directory is already active.");
        }

        if (TrustedPathGuard.IsInsideDirectory(sourceRoot, destinationRoot)
            || TrustedPathGuard.IsInsideDirectory(destinationRoot, sourceRoot))
        {
            throw new InvalidOperationException(
                "The source and destination data directories must not contain each other.");
        }
    }

    private static void ValidateDirectory(string path, bool allowMissing)
    {
        if (!Directory.Exists(path))
        {
            if (File.Exists(path))
            {
                throw new IOException(
                    "A data directory path points to a file.");
            }

            if (allowMissing)
            {
                ValidateExistingAncestors(path);
                return;
            }

            throw new DirectoryNotFoundException(
                "The selected data directory does not exist.");
        }

        FileAttributes attributes = File.GetAttributes(path);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                "Data directories cannot be symbolic links or other reparse points.");
        }

        ValidateExistingAncestors(path);
    }

    private static void ValidateDestinationIsEmpty(string destinationRoot)
    {
        if (Directory.EnumerateFileSystemEntries(destinationRoot).Any())
        {
            throw new IOException("The selected data directory must be empty.");
        }
    }

    private static void ValidateDestinationWritable(string destinationRoot)
    {
        string probePath = Path.Combine(
            destinationRoot,
            $".atomicart-migration-probe-{Guid.NewGuid():N}.tmp");

        try
        {
            using FileStream stream = new(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
            stream.WriteByte(0);
            stream.Flush(flushToDisk: true);
        }
        finally
        {
            if (File.Exists(probePath))
            {
                File.Delete(probePath);
            }
        }
    }

    private static void ValidateAvailableSpace(string destinationRoot, long requiredBytes)
    {
        if (requiredBytes <= 0)
        {
            return;
        }

        string? rootPath = Path.GetPathRoot(destinationRoot);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return;
        }

        try
        {
            DriveInfo drive = new(rootPath);

            if (drive.IsReady && drive.AvailableFreeSpace < requiredBytes)
            {
                throw new IOException(
                    "The selected data directory does not have enough free space.");
            }
        }
        catch (ArgumentException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void EnumerateSource(
        string sourceRoot,
        string currentDirectory,
        ICollection<string> relativeDirectories,
        ICollection<DataRootMigrationFile> files,
        ref long totalBytes,
        CancellationToken ct)
    {
        foreach (string directory in Directory.EnumerateDirectories(currentDirectory))
        {
            ct.ThrowIfCancellationRequested();
            EnsureNotReparsePoint(directory);
            string relativePath = Path.GetRelativePath(sourceRoot, directory);
            relativeDirectories.Add(relativePath);
            EnumerateSource(
                sourceRoot,
                directory,
                relativeDirectories,
                files,
                ref totalBytes,
                ct);
        }

        foreach (string file in Directory.EnumerateFiles(currentDirectory))
        {
            ct.ThrowIfCancellationRequested();
            EnsureNotReparsePoint(file);
            FileInfo fileInfo = new(file);
            string relativePath = Path.GetRelativePath(sourceRoot, file);
            totalBytes = checked(totalBytes + fileInfo.Length);
            files.Add(new DataRootMigrationFile
            {
                RelativePath = relativePath,
                Length = fileInfo.Length,
                CreationTimeUtc = fileInfo.CreationTimeUtc,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                Attributes = fileInfo.Attributes
            });
        }
    }

    private static void EnsureNotReparsePoint(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                "Atomic Art data cannot be moved through symbolic links or other reparse points.");
        }
    }

    private static void ValidateExistingAncestors(string path)
    {
        DirectoryInfo? directory = new DirectoryInfo(path).Parent;

        while (directory is not null)
        {
            if (directory.Exists
                && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "Data directory ancestors cannot be symbolic links or other reparse points.");
            }

            directory = directory.Parent;
        }
    }
}
