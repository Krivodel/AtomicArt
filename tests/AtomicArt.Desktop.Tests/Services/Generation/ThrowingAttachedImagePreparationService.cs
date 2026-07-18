using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal sealed class ThrowingAttachedImagePreparationService : IAttachedImagePreparationService
{
    public Task<AttachedImageDto?> PrepareAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        _ = image;
        _ = selectedModel;
        _ = ct;

        throw new InvalidOperationException("Preparation failed.");
    }
}
