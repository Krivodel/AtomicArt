using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class PassthroughTrustedImageFileService : ITrustedImageFileService
{
    public string? GetTrustedImagePathOrDefault(string? path, string modelId)
    {
        return path;
    }

    public string GetTrustedImagePath(string? path, string modelId)
    {
        return path ?? throw new InvalidOperationException("Image path is required.");
    }

    public void DeleteTrustedImageFileIfExists(
        string? path,
        string modelId,
        Action<string> validateResolvedPath)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            validateResolvedPath(path);
            File.Delete(path);
        }
    }
}
