using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal sealed class SelectiveAttachedImagePreparationService :
    IAttachedImagePreparationService
{
    private readonly string _rejectedFileName;

    public SelectiveAttachedImagePreparationService(string rejectedFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedFileName);

        _rejectedFileName = rejectedFileName;
    }

    public Task<AttachedImageDto?> PrepareAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(selectedModel);
        ct.ThrowIfCancellationRequested();

        AttachedImageDto? result = string.Equals(
            image.FileName,
            _rejectedFileName,
            StringComparison.Ordinal)
            ? null
            : image;

        return Task.FromResult(result);
    }
}
