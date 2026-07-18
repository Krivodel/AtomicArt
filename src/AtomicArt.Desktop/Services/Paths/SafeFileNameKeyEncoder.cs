using System.Security.Cryptography;
using System.Text;

namespace AtomicArt.Desktop.Services.Paths;

internal static class SafeFileNameKeyEncoder
{
    public static string EncodeSha256Hex(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] hashBytes = SHA256.HashData(keyBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
