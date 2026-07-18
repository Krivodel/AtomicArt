using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services;

public sealed class ProtectedDesktopSecretStore : ISecretStore
{
    private const string TrustedPathFailureMessage =
        "Secret path must stay inside Secrets and must not contain reparse points.";
    private const int MaxProtectedSecretFileBytes = 64 * 1024;

    private readonly ConcurrentDictionary<string, string> _temporarySecrets = new();
    private readonly IAtomicArtDataPathProvider? _pathProvider;
    private readonly string _secretsDirectory;
    private readonly ILogger<ProtectedDesktopSecretStore> _logger;

    public ProtectedDesktopSecretStore(IAtomicArtDataPathProvider pathProvider)
        : this(pathProvider, NullLogger<ProtectedDesktopSecretStore>.Instance)
    {
    }

    public ProtectedDesktopSecretStore(
        IAtomicArtDataPathProvider pathProvider,
        ILogger<ProtectedDesktopSecretStore> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _secretsDirectory = pathProvider.SecretsDirectory;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ProtectedDesktopSecretStore(string secretsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretsDirectory);

        _secretsDirectory = Path.GetFullPath(secretsDirectory);
        _logger = NullLogger<ProtectedDesktopSecretStore>.Instance;
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

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
            string[] trustedDirectories = [Path.GetFullPath(_secretsDirectory)];

            if (!TrustedPathGuard.TryOpenTrustedExistingFileForRead(
                path,
                trustedDirectories,
                _secretsDirectory,
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
                    || stream.Length > MaxProtectedSecretFileBytes)
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
            string tempPath = AtomicFileWriteTempPath.CreateHidden(_secretsDirectory, "secret");
            bool secretFileReplaced = false;

            try
            {
                await using (FileStream stream = TrustedPathGuard.CreateTrustedNewFileForWrite(
                    _secretsDirectory,
                    tempPath,
                    TrustedPathFailureMessage))
                {
                    await stream.WriteAsync(protectedBytes, ct).ConfigureAwait(false);
                    await stream.FlushAsync(ct).ConfigureAwait(false);
                }

                TrustedPathGuard.ReplaceTrustedFile(
                    _secretsDirectory,
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

        return Path.Combine(_secretsDirectory, fileName);
    }

    private void EnsureSecretsDirectory()
    {
        if (_pathProvider is not null)
        {
            TrustedPathGuard.EnsureTrustedDirectoryExists(
                _pathProvider,
                _secretsDirectory,
                TrustedPathFailureMessage);
            return;
        }

        TrustedPathGuard.EnsureTrustedDirectoryExists(
            _secretsDirectory,
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
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
    }
}
