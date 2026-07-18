using System.Security.Cryptography;
using System.Text;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ProtectedDesktopSecretStoreTests
{
    [Fact]
    public async Task SetSecretAsync_WithKey_ReadsStoredValue()
    {
        string secretsDirectory = CreateTemporarySecretsDirectory();
        ProtectedDesktopSecretStore store = new(secretsDirectory);
        string key = CreateUniqueKey();
        string value = "value-for-test-only";

        try
        {
            await store.SetSecretAsync(key, value, CancellationToken.None);

            string? storedValue = await store.GetSecretAsync(key, CancellationToken.None);

            storedValue.Should().Be(value);
        }
        finally
        {
            DeleteTemporaryDirectory(secretsDirectory);
        }
    }

    [Fact]
    public async Task SetSecretAsync_WhenWindows_DoesNotPersistPlainText()
    {
        string secretsDirectory = CreateTemporarySecretsDirectory();
        ProtectedDesktopSecretStore store = new(secretsDirectory);
        string key = CreateUniqueKey();
        string value = "value-for-test-only";

        try
        {
            await store.SetSecretAsync(key, value, CancellationToken.None);

            if (OperatingSystem.IsWindows())
            {
                byte[] storedBytes = await File.ReadAllBytesAsync(
                    GetSecretPath(secretsDirectory, key),
                    CancellationToken.None);
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(value);
                string storedHex = Convert.ToHexString(storedBytes);
                string plainTextHex = Convert.ToHexString(plainTextBytes);
                storedHex.Should().NotContain(plainTextHex);
            }

            string? storedValue = await store.GetSecretAsync(key, CancellationToken.None);
            storedValue.Should().Be(value);
        }
        finally
        {
            DeleteTemporaryDirectory(secretsDirectory);
        }
    }

    private static string CreateTemporarySecretsDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "AtomicArt.Tests", Guid.NewGuid().ToString("N"), "Secrets");
    }

    private static string CreateUniqueKey()
    {
        return $"test-{Guid.NewGuid():N}";
    }

    private static string GetSecretPath(string secretsDirectory, string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        string fileName = string.Concat(Convert.ToHexString(SHA256.HashData(keyBytes)), ".bin");

        return Path.Combine(secretsDirectory, fileName);
    }

    private static void DeleteTemporaryDirectory(string secretsDirectory)
    {
        string rootDirectory = Path.GetFullPath(Path.Combine(secretsDirectory, ".."));
        string testsRootDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AtomicArt.Tests"));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!rootDirectory.StartsWith(testsRootDirectory, comparison))
        {
            return;
        }

        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, true);
        }
    }
}
