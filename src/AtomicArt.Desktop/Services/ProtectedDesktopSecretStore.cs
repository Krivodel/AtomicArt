using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services;

public sealed class ProtectedDesktopSecretStore : ISecretStore
{
    private static readonly string TrustedPathFailureMessage =
        TrustedPathGuard.CreateFailureMessage(
            "Secret path",
            AtomicArtPathNames.SecretsDirectory);
    private readonly ConcurrentDictionary<string, string> _temporarySecrets = new();
    private readonly IAtomicArtDataPathProvider? _pathProvider;
    private readonly IDataRootAccessCoordinator? _accessCoordinator;
    private readonly string? _fixedSecretsDirectory;
    private readonly ILogger<ProtectedDesktopSecretStore> _logger;
    private readonly int _maximumProtectedSecretFileBytes;
    private readonly TrustedFileStreamFactory _trustedFileStreamFactory;

    public ProtectedDesktopSecretStore(
        string secretsDirectory,
        IOptions<StorageOptions> options,
        TrustedFileStreamFactory trustedFileStreamFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretsDirectory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(trustedFileStreamFactory);

        _fixedSecretsDirectory = Path.GetFullPath(secretsDirectory);
        _logger = NullLogger<ProtectedDesktopSecretStore>.Instance;
        _maximumProtectedSecretFileBytes =
            options.Value.MaximumProtectedSecretFileBytes;
        _trustedFileStreamFactory = trustedFileStreamFactory;
    }

    public ProtectedDesktopSecretStore(
        IAtomicArtDataPathProvider pathProvider,
        IDataRootAccessCoordinator accessCoordinator,
        IOptions<StorageOptions> options,
        TrustedFileStreamFactory trustedFileStreamFactory)
        : this(
            pathProvider,
            accessCoordinator,
            NullLogger<ProtectedDesktopSecretStore>.Instance,
            options,
            trustedFileStreamFactory)
    {
    }

    public ProtectedDesktopSecretStore(
        IAtomicArtDataPathProvider pathProvider,
        IDataRootAccessCoordinator accessCoordinator,
        ILogger<ProtectedDesktopSecretStore> logger,
        IOptions<StorageOptions> options,
        TrustedFileStreamFactory trustedFileStreamFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(trustedFileStreamFactory);

        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _accessCoordinator = accessCoordinator
            ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maximumProtectedSecretFileBytes =
            options.Value.MaximumProtectedSecretFileBytes;
        _trustedFileStreamFactory = trustedFileStreamFactory;
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        using DataRootAccessLease? accessLease = _accessCoordinator is null
            ? null
            : await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

        if (!OperatingSystem.IsWindows())
        {
            bool found = _temporarySecrets.TryGetValue(key, out string? value);
            _logger.LogWarning(
                "Protected secret storage is unavailable on this platform; process-memory fallback returned secret presence {SecretFound}.",
                found);

            return found ? value : null;
        }

        try
        {
            string path = GetSecretPath(key);
            string secretsDirectory = GetSecretsDirectory();
            string[] trustedDirectories = [secretsDirectory];

            if (!_trustedFileStreamFactory.TryOpenExistingFileForRead(
                path,
                trustedDirectories,
                secretsDirectory,
                TrustedPathFailureMessage,
                out FileStream? stream,
                out string? _))
            {
                return null;
            }

            if (stream is null)
            {
                return null;
            }

            await using (stream.ConfigureAwait(false))
            {
                if (stream.Length <= 0
                    || stream.Length > _maximumProtectedSecretFileBytes)
                {
                    throw new IOException("Protected desktop secret file has an invalid size.");
                }

                int protectedByteCount = checked((int)stream.Length);
                byte[] protectedBytes = new byte[protectedByteCount];
                await stream.ReadExactlyAsync(protectedBytes, ct).ConfigureAwait(false);
                byte[] bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                _logger.LogInformation("Protected desktop secret was read successfully.");

                return Encoding.UTF8.GetString(bytes);
            }
        }
        catch (Exception exception) when (IsSecretStoreException(exception))
        {
            _logger.LogError(exception, "Failed to read protected desktop secret.");
            throw CreateSecretStoreException(exception);
        }
    }

    public async Task SetSecretAsync(string key, string value, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        using DataRootAccessLease? accessLease = _accessCoordinator is null
            ? null
            : await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

        if (!OperatingSystem.IsWindows())
        {
            _temporarySecrets[key] = value;
            _logger.LogWarning(
                "Protected secret storage is unavailable on this platform; secret was retained only in process memory.");
            return;
        }

        try
        {
            EnsureSecretsDirectory();
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            string path = GetSecretPath(key);
            string secretsDirectory = GetSecretsDirectory();
            string tempPath = AtomicFileWriteTempPath.CreateHidden(
                secretsDirectory,
                "secret");
            bool secretFileReplaced = false;

            try
            {
                await using (FileStream stream = _trustedFileStreamFactory.CreateNewFileForWrite(
                    secretsDirectory,
                    tempPath,
                    TrustedPathFailureMessage))
                {
                    await stream.WriteAsync(protectedBytes, ct).ConfigureAwait(false);
                    await stream.FlushAsync(ct).ConfigureAwait(false);
                }

                TrustedPathGuard.ReplaceTrustedFile(
                    secretsDirectory,
                    tempPath,
                    path,
                    TrustedPathFailureMessage);
                secretFileReplaced = true;
                _logger.LogInformation("Protected desktop secret was saved successfully.");
            }
            finally
            {
                if (!secretFileReplaced)
                {
                    DeleteTempFile(tempPath);
                }
            }
        }
        catch (Exception exception) when (IsSecretStoreException(exception))
        {
            _logger.LogError(exception, "Failed to save protected desktop secret.");
            throw CreateSecretStoreException(exception);
        }
    }

    private string GetSecretPath(string key)
    {
        string fileName = string.Concat(SafeFileNameKeyEncoder.EncodeSha256Hex(key), ".bin");

        return Path.Combine(GetSecretsDirectory(), fileName);
    }

    private void EnsureSecretsDirectory()
    {
        if (_pathProvider is not null)
        {
            TrustedPathGuard.EnsureTrustedDirectoryExists(
                _pathProvider,
                GetSecretsDirectory(),
                TrustedPathFailureMessage);
            return;
        }

        TrustedPathGuard.EnsureTrustedDirectoryExists(
            GetSecretsDirectory(),
            directory => Directory.CreateDirectory(directory),
            TrustedPathFailureMessage);
    }

    private static bool IsSecretStoreException(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or CryptographicException
            or NotSupportedException;
    }

    private static InvalidOperationException CreateSecretStoreException(Exception exception)
    {
        return new InvalidOperationException("Failed to access protected desktop secret store.", exception);
    }

    private static void DeleteTempFile(string tempPath)
    {
        FileDeletion.DeleteIfExists(tempPath);
    }

    private string GetSecretsDirectory()
    {
        if (_pathProvider is not null)
        {
            return Path.GetFullPath(_pathProvider.SecretsDirectory);
        }

        return _fixedSecretsDirectory
            ?? throw new InvalidOperationException(
                "A protected secret directory has not been configured.");
    }
}
