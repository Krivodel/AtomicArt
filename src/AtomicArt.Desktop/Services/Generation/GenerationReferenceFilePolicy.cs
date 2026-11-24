using System.Security.Cryptography;
using System.Text;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationReferenceFilePolicy
{
    internal const string OwnerFileExtension = ".ref";

    private const string DirectoryName = "History";
    private const int HashLength = 64;

    internal static string GetDirectory(IAtomicArtDataPathProvider paths)
    {
        return Path.Combine(paths.StateAttachmentsDirectory, DirectoryName);
    }

    internal static async Task<string?> HashAsync(Stream stream, CancellationToken ct)
    {
        byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);

        return Convert.ToHexStringLower(hash);
    }

    internal static bool IsHash(string? hash)
    {
        return hash is { Length: HashLength }
            && hash.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    internal static bool IsSharedImagePath(IAtomicArtDataPathProvider paths, string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && GenerationReferenceFilePolicy.IsHash(Path.GetFileNameWithoutExtension(path))
            && (string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),
                    Path.GetFullPath(GenerationReferenceFilePolicy.GetDirectory(paths)), StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),
                    Path.GetFullPath(paths.ArtDirectory), StringComparison.OrdinalIgnoreCase));
    }

    internal static string GetAlternativePath(IAtomicArtDataPathProvider paths, string path)
    {
        string directory = string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),
            Path.GetFullPath(paths.ArtDirectory), StringComparison.OrdinalIgnoreCase)
            ? GenerationReferenceFilePolicy.GetDirectory(paths)
            : paths.ArtDirectory;

        return Path.Combine(directory, Path.GetFileName(path));
    }

    internal static string? ReadOwnerHash(Stream stream)
    {
        if (stream.Length != HashLength)
        {
            return null;
        }

        byte[] bytes = new byte[HashLength];
        stream.ReadExactly(bytes);
        string hash = Encoding.ASCII.GetString(bytes);

        return GenerationReferenceFilePolicy.IsHash(hash) ? hash : null;
    }
}
