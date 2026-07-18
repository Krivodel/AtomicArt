using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class ExistingFileTrustedImageFileService : ITrustedImageFileService
{
    public string? GetTrustedImagePathOrDefault(string? path, string modelId)
    {
        if (path is not null && File.Exists(path))
        {
            return path;
        }

        return null;
    }

    public string GetTrustedImagePath(string? path, string modelId)
    {
        return GetTrustedImagePathOrDefault(path, modelId)
            ?? throw new InvalidOperationException("Image path is not trusted.");
    }

    public void DeleteTrustedImageFileIfExists(
        string? path,
        string modelId,
        Action<string> validateResolvedPath)
    {
        string trustedPath = GetTrustedImagePath(path, modelId);
        validateResolvedPath(trustedPath);
        File.Delete(trustedPath);
    }
}
