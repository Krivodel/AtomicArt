namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RejectingTrustedImageFileService : TrustedImageFileServiceTestDouble
{
    public override string? GetTrustedImagePathOrDefault(string? path, string modelId)
    {
        return null;
    }

    public override void DeleteTrustedImageFileIfExists(
        string? path,
        string modelId,
        Action<string> validateResolvedPath)
    {
        throw CreateUntrustedPathException();
    }
}
