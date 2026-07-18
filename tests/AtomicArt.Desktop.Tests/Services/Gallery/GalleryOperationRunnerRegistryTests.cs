using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryOperationRunnerRegistryTests
{
    [Fact]
    public void GetRunner_WithRegisteredKind_ReturnsInjectedRunner()
    {
        RecordingRunner runner = new(typeof(AppendBatchGalleryOperation));
        GalleryOperationRunnerRegistry registry = new(
            new List<IGalleryOperationRunner> { runner });

        IGalleryOperationRunner result = registry.GetRunner(typeof(AppendBatchGalleryOperation));

        result.Should().BeSameAs(runner);
    }

    [Fact]
    public void GetRunner_WithMissingKind_Throws()
    {
        GalleryOperationRunnerRegistry registry = new(
            new List<IGalleryOperationRunner>());

        Action act = () => registry.GetRunner(typeof(RemoveGalleryOperation));

        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class RecordingRunner : IGalleryOperationRunner
    {
        public Type OperationType { get; }
        public bool SupportsBatching => OperationType == typeof(AppendBatchGalleryOperation)
                                        || OperationType == typeof(GenerateFrontGalleryOperation);

        public RecordingRunner(Type operationType)
        {
            OperationType = operationType;
        }

        public bool CanRun(IReadOnlyList<GalleryOperation> operations)
        {
            return operations.Any(operation => operation.GetType() == OperationType);
        }

        public IReadOnlyList<GalleryOperation> SelectOperations(IReadOnlyList<GalleryOperation> operations)
        {
            return operations
                .Where(operation => operation.GetType() == OperationType)
                .ToList();
        }

        public Task RunAsync(
            IReadOnlyList<GalleryOperation> operations,
            GalleryOperationCoordinator context,
            CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
