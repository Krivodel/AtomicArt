using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal abstract class AttachedImagePreparationServiceTestDouble :
    IAttachedImagePreparationService
{
    public Task<AttachedImageDto?> PrepareAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(selectedModel);
        ct.ThrowIfCancellationRequested();

        return PrepareCoreAsync(image, selectedModel, ct);
    }

    protected abstract Task<AttachedImageDto?> PrepareCoreAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct);
}
