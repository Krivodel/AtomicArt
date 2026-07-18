using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

internal sealed class EmptyFilePickerService : IFilePickerService
{
    public Task<IReadOnlyList<ImageAttachmentInput>> PickImagesAsync(
        int maxInputBytes,
        CancellationToken ct)
    {
        ImageAttachmentInput[] inputs = [];

        return Task.FromResult<IReadOnlyList<ImageAttachmentInput>>(inputs);
    }
}
