using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal abstract class GalleryOperationRunnerTestDouble : GalleryOperationRunner
{
    public override Type OperationType { get; }
    public override bool SupportsBatching =>
        (OperationType == typeof(AppendBatchGalleryOperation))
        || (OperationType == typeof(GenerateFrontGalleryOperation));

    protected GalleryOperationRunnerTestDouble(Type operationType)
    {
        OperationType = operationType ?? throw new ArgumentNullException(nameof(operationType));
    }
}
