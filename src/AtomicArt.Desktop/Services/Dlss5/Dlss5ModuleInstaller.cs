using System.IO.Compression;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Dlss5;

public sealed class Dlss5ModuleInstaller : IDlss5ModuleInstaller
{
    public bool IsInstalled => VerifyInstalledRuntime();

    private const int IoBufferSize = 128 * 1024;
    private const int ExclusiveInstallCount = 1;

    private readonly HttpClient _httpClient;
    private readonly Dlss5ModulePaths _paths;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly ILogger<Dlss5ModuleInstaller> _logger;
    private readonly SemaphoreSlim _installLock = new(ExclusiveInstallCount, ExclusiveInstallCount);

    public Dlss5ModuleInstaller(
        HttpClient httpClient,
        Dlss5ModulePaths paths,
        IDataRootAccessCoordinator accessCoordinator,
        ILogger<Dlss5ModuleInstaller> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _accessCoordinator = accessCoordinator ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureInstalledAsync(
        IProgress<Dlss5ModuleInstallProgress>? progress,
        CancellationToken ct)
    {
        if (IsInstalled)
        {
            return;
        }

        await _installLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (IsInstalled)
            {
                return;
            }

            using DataRootAccessLease accessLease = await _accessCoordinator
                .AcquireAccessAsync(ct)
                .ConfigureAwait(false);

            Directory.CreateDirectory(_paths.ModuleDirectory);
            string temporaryArchivePath = Path.Combine(
                _paths.ModuleDirectory,
                $".download-{Guid.NewGuid():N}.partial");
            string stagingDirectory = Path.Combine(
                _paths.ModuleDirectory,
                $".install-{Guid.NewGuid():N}");

            try
            {
                await DownloadArchiveAsync(temporaryArchivePath, progress, ct)
                    .ConfigureAwait(false);
                await Dlss5ModuleInstaller.ExtractRuntimeAsync(temporaryArchivePath, stagingDirectory, ct)
                    .ConfigureAwait(false);
                Dlss5ModuleInstaller.VerifyRuntimeDirectory(stagingDirectory);
                PublishRuntime(stagingDirectory);
                _logger.LogInformation(
                    "DLSS 5 module version {ModuleVersion} was installed.",
                    Dlss5FeatureDefinition.Version);
            }
            finally
            {
                Dlss5ModuleInstaller.DeleteFileIfExists(temporaryArchivePath);
                Dlss5ModuleInstaller.DeleteDirectoryIfExists(stagingDirectory);
            }
        }
        finally
        {
            _installLock.Release();
        }
    }

    internal void PublishRuntime(string stagingDirectory)
    {
        string destinationDirectory = _paths.RuntimeDirectory;
        string? runtimeParentDirectory = Path.GetDirectoryName(destinationDirectory);

        if (string.IsNullOrWhiteSpace(runtimeParentDirectory))
        {
            throw new InvalidOperationException("The DLSS 5 runtime destination is invalid.");
        }

        Directory.CreateDirectory(runtimeParentDirectory);
        string previousDirectory = $"{destinationDirectory}.previous";

        Dlss5ModuleInstaller.DeleteDirectoryIfExists(previousDirectory);

        if (Directory.Exists(destinationDirectory))
        {
            Directory.Move(destinationDirectory, previousDirectory);
        }

        try
        {
            Directory.Move(stagingDirectory, destinationDirectory);
        }
        catch (IOException)
        {
            Dlss5ModuleInstaller.RestorePreviousRuntime(previousDirectory, destinationDirectory);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            Dlss5ModuleInstaller.RestorePreviousRuntime(previousDirectory, destinationDirectory);
            throw;
        }

        Dlss5ModuleInstaller.DeleteDirectoryIfExists(previousDirectory);
    }

    private static async Task ExtractRuntimeAsync(
        string archivePath,
        string stagingDirectory,
        CancellationToken ct)
    {
        Directory.CreateDirectory(stagingDirectory);

        using ZipArchive archive = ZipFile.OpenRead(archivePath);

        Dictionary<string, Dlss5RuntimeFile> expectedFiles = Dlss5FeatureDefinition.RuntimeFiles
            .ToDictionary(file => file.ArchivePath, StringComparer.Ordinal);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (!expectedFiles.TryGetValue(entry.FullName, out Dlss5RuntimeFile? expected))
            {
                continue;
            }

            string relativePath = expected.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            string destinationPath = Path.GetFullPath(Path.Combine(stagingDirectory, relativePath));
            string stagingRoot = Path.GetFullPath(stagingDirectory + Path.DirectorySeparatorChar);
            if (!destinationPath.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The DLSS 5 archive contains an invalid runtime path.");
            }

            string? parentDirectory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                throw new InvalidDataException("The DLSS 5 archive contains an invalid runtime path.");
            }

            Directory.CreateDirectory(parentDirectory);
            await using Stream source = entry.Open();
            await using FileStream destination = new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            await source.CopyToAsync(destination, ct).ConfigureAwait(false);
            await destination.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    private static void VerifyRuntimeDirectory(string directoryPath)
    {
        foreach (Dlss5RuntimeFile expected in Dlss5FeatureDefinition.RuntimeFiles)
        {
            string filePath = Path.Combine(
                directoryPath,
                expected.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            FileInfo fileInfo = new(filePath);

            if (!fileInfo.Exists)
            {
                if (expected.IsOptional)
                {
                    continue;
                }

                throw new InvalidDataException("The DLSS 5 module is incomplete or corrupted.");
            }

            if (fileInfo.Length != expected.Length)
            {
                throw new InvalidDataException("The DLSS 5 module is incomplete or corrupted.");
            }

            if (expected.Sha256 is null)
            {
                continue;
            }

            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            string actualHash = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actualHash, expected.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The DLSS 5 module integrity check failed.");
            }
        }
    }

    private static void RestorePreviousRuntime(string previousDirectory, string destinationDirectory)
    {
        if ((Directory.Exists(previousDirectory)) && (!Directory.Exists(destinationDirectory)))
        {
            Directory.Move(previousDirectory, destinationDirectory);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private async Task DownloadArchiveAsync(
        string destinationPath,
        IProgress<Dlss5ModuleInstallProgress>? progress,
        CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Dlss5FeatureDefinition.DownloadUrl);
        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength
            ?? Dlss5FeatureDefinition.DownloadSizeBytes;
        progress?.Report(new Dlss5ModuleInstallProgress(0, totalBytes, totalBytes <= 0));

        await using Stream source = await response.Content.ReadAsStreamAsync(ct)
            .ConfigureAwait(false);
        long receivedBytes = 0;
        {
            await using FileStream destination = new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: IoBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            byte[] buffer = GC.AllocateUninitializedArray<byte>(IoBufferSize);
            while (true)
            {
                int read = await source.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                receivedBytes += read;
                progress?.Report(new Dlss5ModuleInstallProgress(
                    receivedBytes,
                    totalBytes,
                    totalBytes <= 0));
            }

            await destination.FlushAsync(ct).ConfigureAwait(false);
        }

        if (receivedBytes != Dlss5FeatureDefinition.DownloadSizeBytes)
        {
            throw new InvalidDataException("The DLSS 5 module archive has an unexpected size.");
        }

        await using FileStream verificationStream = new(
            destinationPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: IoBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        string actualHash = Convert.ToHexString(
            await SHA256.HashDataAsync(verificationStream, ct).ConfigureAwait(false));
        if (!string.Equals(actualHash, Dlss5FeatureDefinition.DownloadSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The DLSS 5 module archive integrity check failed.");
        }
    }

    private bool VerifyInstalledRuntime()
    {
        try
        {
            Dlss5ModuleInstaller.VerifyRuntimeDirectory(_paths.RuntimeDirectory);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }
}
