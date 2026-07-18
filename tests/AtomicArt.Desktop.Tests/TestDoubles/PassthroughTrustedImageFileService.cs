namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class PassthroughTrustedImageFileService : TrustedImageFileServiceTestDouble
{
    public override string? GetTrustedImagePathOrDefault(string? path, string modelId)
    {
        return path;
    }

    public override string GetTrustedImagePath(string? path, string modelId)
    {
        return path ?? throw new InvalidOperationException("Image path is required.");
    }

    public override void DeleteTrustedImageFileIfExists(
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
