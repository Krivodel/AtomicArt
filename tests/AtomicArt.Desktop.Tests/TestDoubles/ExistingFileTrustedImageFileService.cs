namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class ExistingFileTrustedImageFileService : TrustedImageFileServiceTestDouble
{
    public override string? GetTrustedImagePathOrDefault(string? path, string modelId)
    {
        if (path is not null && File.Exists(path))
        {
            return path;
        }

        return null;
    }

    public override void DeleteTrustedImageFileIfExists(
        string? path,
        string modelId,
        Action<string> validateResolvedPath)
    {
        string trustedPath = GetTrustedImagePath(path, modelId);
        validateResolvedPath(trustedPath);
        File.Delete(trustedPath);
    }
}
