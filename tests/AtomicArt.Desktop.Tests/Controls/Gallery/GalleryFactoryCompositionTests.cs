using FluentAssertions;
using Xunit;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public sealed class GalleryFactoryCompositionTests
{
    [Fact]
    public void FactorySources_WithProductionComposition_DoNotUseServiceLocator()
    {
        string root = FindWorkspaceRoot();
        string factoryDirectory = Path.Combine(
            root,
            "src",
            "AtomicArt.Desktop",
            "Controls",
            "Gallery");
        IReadOnlyList<string> factorySources = Directory
            .EnumerateFiles(factoryDirectory, "*Factory.cs")
            .Select(File.ReadAllText)
            .ToList();
        string allFactorySources = string.Join(Environment.NewLine, factorySources);

        allFactorySources.Should().NotContain("IServiceProvider");
        allFactorySources.Should().NotContain("GetRequiredService");
        allFactorySources.Should().NotContain("ActivatorUtilities");
        allFactorySources.Should().NotContain("BuildServiceProvider");
        allFactorySources.Should().NotContain("CreateStandalone");
        allFactorySources.Should().NotContain("new GalleryLayoutService");
        allFactorySources.Should().NotContain("new GalleryAnimationScheduler");
        allFactorySources.Should().NotContain("new GalleryOverlayEffects");
        allFactorySources.Should().NotContain("new GalleryMotionAnimator");
        allFactorySources.Should().NotContain("new GalleryAppendRunner");
        allFactorySources.Should().NotContain("new GalleryFrontGenerationRunner");
        allFactorySources.Should().NotContain("new GalleryRemoveRunner");
        allFactorySources.Should().NotContain("new GalleryMixedMutationRunner");
        allFactorySources.Should().NotContain("new GalleryOperationRunnerRegistry");
        allFactorySources.Should().NotContain("new GalleryOperationCoordinator");
        allFactorySources.Should().NotContain("GalleryLayoutFactory");
        allFactorySources.Should().NotContain("AnimationSchedulerFactory");
        allFactorySources.Should().NotContain("OverlayEffectsFactory");
        allFactorySources.Should().NotContain("MotionAnimatorFactory");
        allFactorySources.Should().NotContain("OperationRunnersFactory");
        allFactorySources.Should().NotContain("RunnerRegistryFactory");
        allFactorySources.Should().NotContain("OperationCoordinatorFactory");
    }

    private static string FindWorkspaceRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "src",
                "AtomicArt.Desktop",
                "Controls",
                "Gallery",
                "GallerySceneServicesFactory.cs");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("AtomicArt workspace root was not found.");
    }
}
