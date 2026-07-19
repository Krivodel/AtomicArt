using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal sealed class SelectiveAttachedImagePreparationService :
    AttachedImagePreparationServiceTestDouble
{
    private readonly string _rejectedFileName;

    public SelectiveAttachedImagePreparationService(string rejectedFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedFileName);

        _rejectedFileName = rejectedFileName;
    }

    protected override Task<AttachedImageDto?> PrepareCoreAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        AttachedImageDto? result = string.Equals(
            image.FileName,
            _rejectedFileName,
            StringComparison.Ordinal)
            ? null
            : image;

        return Task.FromResult(result);
    }
}
