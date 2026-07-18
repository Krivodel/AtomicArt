using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class AnimatedGalleryOperationsTests
{
    [Fact]
    public async Task GenerateFrontAsync_WhenNoSceneAttached_CompletesWithoutDelegating()
    {
        AnimatedGalleryOperations operations = CreateOperations();
        List<object> items = [Guid.NewGuid()];

        await operations.GenerateFrontAsync(items, CancellationToken.None);

        items.Should().ContainSingle();
    }

    [Fact]
    public async Task GenerateFrontAsync_WhenSceneAttached_DelegatesToActiveOperations()
    {
        AnimatedGalleryOperations operations = CreateOperations();
        RecordingOperations sceneOperations = new();
        IAnimatedGalleryOperationsRegistration registration = operations;
        List<object> items = [Guid.NewGuid()];
        registration.Attach(sceneOperations);

        await operations.GenerateFrontAsync(items, CancellationToken.None);

        sceneOperations.GenerateFrontCallCount.Should().Be(1);
        sceneOperations.LastGenerateFrontItems.Should().Equal(items);
    }

    [Fact]
    public async Task RemoveAsync_AfterSceneDetached_CompletesWithoutDelegating()
    {
        AnimatedGalleryOperations operations = CreateOperations();
        RecordingOperations sceneOperations = new();
        IAnimatedGalleryOperationsRegistration registration = operations;
        Guid itemId = Guid.NewGuid();
        registration.Attach(sceneOperations);
        registration.Detach(sceneOperations);

        await operations.RemoveAsync(itemId, CancellationToken.None);

        sceneOperations.RemoveCallCount.Should().Be(0);
    }

    [Fact]
    public async Task RestoreSnapshotAsync_WhenSceneAttached_DelegatesToActiveOperations()
    {
        AnimatedGalleryOperations operations = CreateOperations();
        RecordingOperations sceneOperations = new();
        IAnimatedGalleryOperationsRegistration registration = operations;
        List<object> items = [Guid.NewGuid()];
        registration.Attach(sceneOperations);

        await operations.RestoreSnapshotAsync(items, CancellationToken.None);

        sceneOperations.RestoreSnapshotCallCount.Should().Be(1);
        sceneOperations.LastRestoreSnapshotItems.Should().Equal(items);
    }

    private static AnimatedGalleryOperations CreateOperations()
    {
        GallerySceneServicesFactory servicesFactory = new(CreateScene);
        IAnimatedGallerySceneFactory sceneFactory = new AnimatedGallerySceneFactory(servicesFactory);

        return new AnimatedGalleryOperations(
            sceneFactory,
            NullLogger<AnimatedGalleryOperations>.Instance);
    }

    private static AnimatedGalleryScene CreateScene(TopLevel topLevel)
    {
        return AnimatedGallerySceneTestFactory.Create(topLevel);
    }

    private sealed class RecordingOperations : IAnimatedGalleryOperations
    {
        public int GenerateFrontCallCount => _generateFrontCallCount;
        public int RemoveCallCount => _removeCallCount;
        public int RestoreSnapshotCallCount => _restoreSnapshotCallCount;
        public IReadOnlyList<object> LastGenerateFrontItems => _lastGenerateFrontItems;
        public IReadOnlyList<object> LastRestoreSnapshotItems => _lastRestoreSnapshotItems;

        private int _generateFrontCallCount;
        private int _removeCallCount;
        private int _restoreSnapshotCallCount;
        private IReadOnlyList<object> _lastGenerateFrontItems = [];
        private IReadOnlyList<object> _lastRestoreSnapshotItems = [];

        public Task AppendBatchAsync(IReadOnlyList<object> items, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(items);
            ct.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public Task GenerateFrontAsync(IReadOnlyList<object> items, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(items);
            ct.ThrowIfCancellationRequested();

            _generateFrontCallCount++;
            _lastGenerateFrontItems = items.ToArray();

            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid itemId, CancellationToken ct)
        {
            _ = itemId;
            ct.ThrowIfCancellationRequested();

            _removeCallCount++;

            return Task.CompletedTask;
        }

        public Task ApplyMixedMutationAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(finalItems);
            ct.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public Task RestoreSnapshotAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(finalItems);
            ct.ThrowIfCancellationRequested();

            _restoreSnapshotCallCount++;
            _lastRestoreSnapshotItems = finalItems.ToArray();

            return Task.CompletedTask;
        }
    }
}
