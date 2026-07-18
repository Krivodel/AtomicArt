using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal static class GalleryOperationCompletion
{
    internal static void Complete(IEnumerable<GalleryOperation> operations)
    {
        foreach (GalleryOperation operation in operations)
        {
            operation.Completion.TrySetResult();
        }
    }

    internal static void Cancel(
        IEnumerable<GalleryOperation> operations,
        CancellationToken ct)
    {
        foreach (GalleryOperation operation in operations)
        {
            operation.Completion.TrySetCanceled(ct);
        }
    }

    internal static void Fail(
        IEnumerable<GalleryOperation> operations,
        Exception exception)
    {
        foreach (GalleryOperation operation in operations)
        {
            operation.Completion.TrySetException(exception);
        }
    }
}
