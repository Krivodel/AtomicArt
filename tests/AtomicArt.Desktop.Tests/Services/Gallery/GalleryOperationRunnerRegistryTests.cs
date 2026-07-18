using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Tests.TestDoubles;

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

    private sealed class RecordingRunner : GalleryOperationRunnerTestDouble
    {
        public RecordingRunner(Type operationType)
            : base(operationType)
        {
        }

        public override Task RunAsync(
            IReadOnlyList<GalleryOperation> operations,
            GalleryOperationCoordinator context,
            CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
