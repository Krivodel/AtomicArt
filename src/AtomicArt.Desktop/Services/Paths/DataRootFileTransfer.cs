using System.Buffers;
using System.Security.Cryptography;

using Microsoft.Extensions.Options;

namespace AtomicArt.Desktop.Services.Paths;

internal sealed class DataRootFileTransfer
{
    private readonly int _copyBufferSize;

    public DataRootFileTransfer(IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _copyBufferSize = options.Value.DataRootFileTransferBufferSize;
    }

    internal async Task<IReadOnlyList<DataRootMigrationFile>> CopyAndVerifyAsync(
        DataRootMigrationPlan plan,
        IProgress<DataRootMigrationProgress> progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(progress);

        CreateDestinationDirectories(plan);
        List<DataRootMigrationFile> verifiedFiles = [];
        long completedWorkBytes = 0;
        long totalWorkBytes = checked(plan.TotalBytes * 3);

        for (int index = 0; index < plan.Files.Count; index++)
        {
            DataRootMigrationFile file = plan.Files[index];
            string sourcePath = ResolveOwnedPath(
                plan.SourceRootDirectory,
                file.RelativePath);
            string destinationPath = ResolveOwnedPath(
                plan.DestinationRootDirectory,
                file.RelativePath);
            string sourceHash = await CopyFileAsync(
                sourcePath,
                destinationPath,
                copiedBytes =>
                {
                    Report(
                        progress,
                        DataRootMigrationProgressStage.Copying,
                        completedWorkBytes + copiedBytes,
                        totalWorkBytes,
                        index,
                        plan.Files.Count);
                },
                ct).ConfigureAwait(false);
            completedWorkBytes = checked(completedWorkBytes + file.Length);
            string destinationHash = await HashFileAsync(
                destinationPath,
                verifiedBytes =>
                {
                    Report(
                        progress,
                        DataRootMigrationProgressStage.Verifying,
                        completedWorkBytes + verifiedBytes,
                        totalWorkBytes,
                        index,
                        plan.Files.Count);
                },
                ct).ConfigureAwait(false);

            if (!string.Equals(sourceHash, destinationHash, StringComparison.Ordinal)
                || new FileInfo(destinationPath).Length != file.Length)
            {
                throw new IOException(
                    "A copied Atomic Art data file failed verification.");
            }

            File.SetCreationTimeUtc(destinationPath, file.CreationTimeUtc);
            File.SetLastWriteTimeUtc(destinationPath, file.LastWriteTimeUtc);
            File.SetAttributes(
                destinationPath,
                file.Attributes & ~FileAttributes.ReparsePoint);
            completedWorkBytes = checked(completedWorkBytes + file.Length);
            verifiedFiles.Add(file with { Sha256 = sourceHash });
            Report(
                progress,
                DataRootMigrationProgressStage.Verifying,
                completedWorkBytes,
                totalWorkBytes,
                index + 1,
                plan.Files.Count);
        }

        await VerifySourceUnchangedAsync(
            plan,
            verifiedFiles,
            completedWorkBytes,
            totalWorkBytes,
            progress,
            ct).ConfigureAwait(false);
        ValidateDestinationContents(plan, verifiedFiles);

        return verifiedFiles;
    }

    internal void DeleteCopiedFiles(
        string rootDirectory,
        IReadOnlyList<DataRootMigrationFile> files,
        IReadOnlyList<string>? relativeDirectories = null)
    {
        DeleteFiles(
            rootDirectory,
            files,
            relativeDirectories,
            verifyFiles: false,
            deleteRootWhenEmpty: false);
    }

    internal void DeleteSourceFiles(
        string rootDirectory,
        IReadOnlyList<DataRootMigrationFile> files,
        IReadOnlyList<string>? relativeDirectories = null)
    {
        DeleteFiles(
            rootDirectory,
            files,
            relativeDirectories,
            verifyFiles: true,
            deleteRootWhenEmpty: true);
    }

    internal void DeleteVerifiedCopiedFiles(
        string rootDirectory,
        IReadOnlyList<DataRootMigrationFile> files,
        IReadOnlyList<string>? relativeDirectories = null)
    {
        DeleteFiles(
            rootDirectory,
            files,
            relativeDirectories,
            verifyFiles: true,
            deleteRootWhenEmpty: false);
    }

    internal bool FilesMatchManifest(
        string rootDirectory,
        IReadOnlyList<DataRootMigrationFile> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(files);

        try
        {
            string root = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(rootDirectory));

            if (!Directory.Exists(root)
                || !FileSetsMatch(root, files))
            {
                return false;
            }

            foreach (DataRootMigrationFile file in files)
            {
                string path = ResolveOwnedPath(root, file.RelativePath);

                if (!File.Exists(path))
                {
                    return false;
                }

                EnsureNotReparsePoint(path);
                EnsureFileMatchesManifest(path, file);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return false;
        }
    }

    private void DeleteFiles(
        string rootDirectory,
        IReadOnlyList<DataRootMigrationFile> files,
        IReadOnlyList<string>? relativeDirectories,
        bool verifyFiles,
        bool deleteRootWhenEmpty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(files);

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));

        foreach (DataRootMigrationFile file in files)
        {
            string path = ResolveOwnedPath(root, file.RelativePath);

            if (File.Exists(path))
            {
                EnsureNotReparsePoint(path);

                if (verifyFiles)
                {
                    EnsureFileMatchesManifest(path, file);
                }

                File.Delete(path);
            }
        }

        IEnumerable<string> directories = relativeDirectories
            ?? files
                .Select(file => Path.GetDirectoryName(file.RelativePath))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path ?? string.Empty);

        foreach (string relativeDirectory in directories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => path.Length))
        {
            string directory = ResolveOwnedPath(root, relativeDirectory);

            if (Directory.Exists(directory)
                && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                EnsureNotReparsePoint(directory);
                Directory.Delete(directory);
            }
        }

        if (deleteRootWhenEmpty
            && Directory.Exists(root)
            && !Directory.EnumerateFileSystemEntries(root).Any())
        {
            EnsureNotReparsePoint(root);
            Directory.Delete(root);
        }
    }

    private void CreateDestinationDirectories(DataRootMigrationPlan plan)
    {
        foreach (string relativeDirectory in plan.RelativeDirectories)
        {
            string destinationDirectory = ResolveOwnedPath(
                plan.DestinationRootDirectory,
                relativeDirectory);
            Directory.CreateDirectory(destinationDirectory);
        }
    }

    private async Task<string> CopyFileAsync(
        string sourcePath,
        string destinationPath,
        Action<long> reportCopiedBytes,
        CancellationToken ct)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(_copyBufferSize);
        long copiedBytes = 0;

        try
        {
            await using FileStream source = new(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                _copyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using FileStream destination = new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                _copyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            while (true)
            {
                int bytesRead = await source
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), ct)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                await destination
                    .WriteAsync(buffer.AsMemory(0, bytesRead), ct)
                    .ConfigureAwait(false);
                hash.AppendData(buffer, 0, bytesRead);
                copiedBytes = checked(copiedBytes + bytesRead);
                reportCopiedBytes(copiedBytes);
            }

            await destination.FlushAsync(ct).ConfigureAwait(false);
            destination.Flush(flushToDisk: true);

            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<string> HashFileAsync(
        string path,
        Action<long> reportVerifiedBytes,
        CancellationToken ct)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(_copyBufferSize);
        long verifiedBytes = 0;

        try
        {
            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                _copyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            while (true)
            {
                int bytesRead = await stream
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), ct)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                hash.AppendData(buffer, 0, bytesRead);
                verifiedBytes = checked(verifiedBytes + bytesRead);
                reportVerifiedBytes(verifiedBytes);
            }

            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task VerifySourceUnchangedAsync(
        DataRootMigrationPlan plan,
        IReadOnlyList<DataRootMigrationFile> verifiedFiles,
        long completedWorkBytes,
        long totalWorkBytes,
        IProgress<DataRootMigrationProgress> progress,
        CancellationToken ct)
    {
        long verifiedSourceBytes = 0;

        for (int index = 0; index < verifiedFiles.Count; index++)
        {
            DataRootMigrationFile file = verifiedFiles[index];
            string sourcePath = ResolveOwnedPath(
                plan.SourceRootDirectory,
                file.RelativePath);
            string sourceHash = await HashFileAsync(
                sourcePath,
                fileBytes =>
                {
                    Report(
                        progress,
                        DataRootMigrationProgressStage.Verifying,
                        completedWorkBytes + verifiedSourceBytes + fileBytes,
                        totalWorkBytes,
                        index,
                        verifiedFiles.Count);
                },
                ct).ConfigureAwait(false);

            if (!string.Equals(sourceHash, file.Sha256, StringComparison.Ordinal)
                || new FileInfo(sourcePath).Length != file.Length)
            {
                throw new IOException(
                    "Atomic Art data changed while it was being copied.");
            }

            verifiedSourceBytes = checked(verifiedSourceBytes + file.Length);
        }
    }

    private void ValidateDestinationContents(
        DataRootMigrationPlan plan,
        IReadOnlyList<DataRootMigrationFile> verifiedFiles)
    {
        if (!FileSetsMatch(plan.DestinationRootDirectory, verifiedFiles))
        {
            throw new IOException(
                "The selected data directory changed while data was being copied.");
        }
    }

    private bool FileSetsMatch(
        string rootDirectory,
        IReadOnlyList<DataRootMigrationFile> files)
    {
        HashSet<string> expectedFiles = files
            .Select(file => NormalizeRelativePath(file.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> actualFiles = Directory
            .EnumerateFiles(
                rootDirectory,
                "*",
                SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(
                Path.GetRelativePath(rootDirectory, path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return expectedFiles.SetEquals(actualFiles);
    }

    private string NormalizeRelativePath(string relativePath)
    {
        return relativePath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private string ResolveOwnedPath(string rootDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath))
        {
            throw new IOException("A data migration path is invalid.");
        }

        string fullPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));

        if (!TrustedPathGuard.IsInsideDirectory(rootDirectory, fullPath))
        {
            throw new IOException("A data migration path escaped its data root.");
        }

        return fullPath;
    }

    private void EnsureNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                "Atomic Art data cannot be cleaned through a reparse point.");
        }
    }

    private void EnsureFileMatchesManifest(
        string path,
        DataRootMigrationFile file)
    {
        FileInfo fileInfo = new(path);

        if (fileInfo.Length != file.Length)
        {
            throw new IOException(
                "A data file changed before migration cleanup completed.");
        }

        if (string.IsNullOrWhiteSpace(file.Sha256))
        {
            return;
        }

        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            _copyBufferSize,
            FileOptions.SequentialScan);
        byte[] hash = SHA256.HashData(stream);
        string hashText = Convert.ToHexString(hash);

        if (!string.Equals(hashText, file.Sha256, StringComparison.Ordinal))
        {
            throw new IOException(
                "A data file changed before migration cleanup completed.");
        }
    }

    private void Report(
        IProgress<DataRootMigrationProgress> progress,
        DataRootMigrationProgressStage stage,
        long completedBytes,
        long totalBytes,
        int completedFiles,
        int totalFiles)
    {
        progress.Report(new DataRootMigrationProgress
        {
            Stage = stage,
            CompletedBytes = completedBytes,
            TotalBytes = totalBytes,
            CompletedFiles = completedFiles,
            TotalFiles = totalFiles
        });
    }
}
