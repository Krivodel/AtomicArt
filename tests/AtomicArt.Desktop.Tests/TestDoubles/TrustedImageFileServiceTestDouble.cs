using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal abstract class TrustedImageFileServiceTestDouble : ITrustedImageFileService
{
    public abstract string? GetTrustedImagePathOrDefault(string? path, string modelId);

    public virtual string GetTrustedImagePath(string? path, string modelId)
    {
        return GetRequiredTrustedPath(GetTrustedImagePathOrDefault(path, modelId));
    }

    public virtual void DeleteTrustedImageFileIfExists(
        string? path,
        string modelId,
        Action<string> validateResolvedPath)
    {
        throw CreateUntrustedPathException();
    }

    protected static InvalidOperationException CreateUntrustedPathException()
    {
        return new InvalidOperationException("Image path is not trusted.");
    }

    protected static string GetRequiredTrustedPath(string? path)
    {
        return path ?? throw CreateUntrustedPathException();
    }
}
