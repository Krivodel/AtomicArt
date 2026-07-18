using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Tests.TestDoubles;
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
        RecordingAnimatedGalleryOperations sceneOperations = new();
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
        RecordingAnimatedGalleryOperations sceneOperations = new();
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
        RecordingAnimatedGalleryOperations sceneOperations = new();
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
}
