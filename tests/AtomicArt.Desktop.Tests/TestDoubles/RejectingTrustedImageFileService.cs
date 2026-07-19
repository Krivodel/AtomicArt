namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RejectingTrustedImageFileService : TrustedImageFileServiceTestDouble
{
    public override string? GetTrustedImagePathOrDefault(string? path, string modelId)
    {
        return null;
    }

}
