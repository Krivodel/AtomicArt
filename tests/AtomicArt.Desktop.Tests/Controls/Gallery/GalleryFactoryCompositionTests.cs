using FluentAssertions;
using Xunit;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public sealed class GalleryFactoryCompositionTests
{
    private static readonly string[] ForbiddenFactorySourceFragments =
    [
        "IServiceProvider",
        "GetRequiredService",
        "ActivatorUtilities",
        "BuildServiceProvider",
        "CreateStandalone",
        "new GalleryLayoutService",
        "new GalleryAnimationScheduler",
        "new GalleryOverlayEffects",
        "new GalleryMotionAnimator",
        "new GalleryAppendRunner",
        "new GalleryFrontGenerationRunner",
        "new GalleryRemoveRunner",
        "new GalleryMixedMutationRunner",
        "new GalleryOperationRunnerRegistry",
        "new GalleryOperationCoordinator",
        "GalleryLayoutFactory",
        "AnimationSchedulerFactory",
        "OverlayEffectsFactory",
        "MotionAnimatorFactory",
        "OperationRunnersFactory",
        "RunnerRegistryFactory",
        "OperationCoordinatorFactory"
    ];

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

        foreach (string forbiddenFragment in ForbiddenFactorySourceFragments)
        {
            allFactorySources.Should().NotContain(forbiddenFragment);
        }
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
