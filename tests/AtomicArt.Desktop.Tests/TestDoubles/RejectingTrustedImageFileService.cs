using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RejectingTrustedImageFileService : ITrustedImageFileService
{
    public string? GetTrustedImagePathOrDefault(string? path, string modelId)
    {
        return null;
    }

    public string GetTrustedImagePath(string? path, string modelId)
    {
        throw new InvalidOperationException("Image path is not trusted.");
    }

    public void DeleteTrustedImageFileIfExists(
        string? path,
        string modelId,
        Action<string> validateResolvedPath)
    {
        throw new InvalidOperationException("Image path is not trusted.");
    }
}
