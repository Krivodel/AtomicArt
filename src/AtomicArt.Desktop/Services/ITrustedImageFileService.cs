namespace AtomicArt.Desktop.Services;

public interface ITrustedImageFileService
{
    string? GetTrustedImagePathOrDefault(string? path, string modelId);

    string GetTrustedImagePath(string? path, string modelId);

    void DeleteTrustedImageFileIfExists(
        string? path,
        string modelId,
        Action<string> validateResolvedPath);
}
