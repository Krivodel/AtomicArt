using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal sealed class PassThroughAttachedImagePreparationService :
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

        return Task.FromResult<AttachedImageDto?>(image);
    }
}
