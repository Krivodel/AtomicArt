using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal sealed class PassThroughAttachedImagePreparationService :
    AttachedImagePreparationServiceTestDouble
{
    protected override Task<AttachedImageDto?> PrepareCoreAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        return Task.FromResult<AttachedImageDto?>(image);
    }
}
