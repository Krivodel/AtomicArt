using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Paths;
using static AtomicArt.Desktop.Tests.Common.DesktopTestDirectories;

namespace AtomicArt.Desktop.Tests.Services.Dlss5;

public sealed class Dlss5ModuleInstallerTests
{
    [Fact]
    public void RuntimeFiles_ContainsDynamicAddonAsRequiredRuntimeFile()
    {
        Dlss5RuntimeFile? dynamicAddon = Dlss5FeatureDefinition.RuntimeFiles
            .SingleOrDefault(file => string.Equals(
                file.RelativePath,
                "dlssnr/dlss5-event-dynamic.addon64",
                StringComparison.Ordinal));

        dynamicAddon.Should().NotBeNull();
        Dlss5RuntimeFile dynamicAddonValue = dynamicAddon
            ?? throw new InvalidOperationException("The dynamic DLSS 5 addon is not defined.");
        dynamicAddonValue.Length.Should().Be(288_256);
        dynamicAddonValue.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void DynamicAddonPath_UsesInstalledModuleRuntimeDirectory()
    {
        string rootDirectory = CreateCleanDirectory(
            nameof(DynamicAddonPath_UsesInstalledModuleRuntimeDirectory));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        Dlss5ModulePaths paths = new(pathProvider);

        paths.DynamicAddonPath.Should().Be(
            Path.Combine(paths.DlssnrDirectory, "dlss5-event-dynamic.addon64"));
    }

    [Fact]
    public void PublishRuntime_WhenRuntimeParentDoesNotExist_CreatesParentAndPublishesStagingDirectory()
    {
        string rootDirectory = CreateCleanDirectory(
            nameof(PublishRuntime_WhenRuntimeParentDoesNotExist_CreatesParentAndPublishesStagingDirectory));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        Dlss5ModulePaths paths = new(pathProvider);
        string stagingDirectory = Path.Combine(rootDirectory, "staging");
        Directory.CreateDirectory(stagingDirectory);
        string stagedFilePath = Path.Combine(stagingDirectory, "host", "nvngx.dll");
        string stagedFileDirectory = Path.GetDirectoryName(stagedFilePath)
            ?? throw new InvalidOperationException("The staged test file directory could not be resolved.");
        Directory.CreateDirectory(stagedFileDirectory);
        File.WriteAllBytes(stagedFilePath, [1, 2, 3]);
        Dlss5ModuleInstaller installer = new(
            new HttpClient(),
            paths,
            new DataRootAccessCoordinator(),
            NullLogger<Dlss5ModuleInstaller>.Instance);

        installer.PublishRuntime(stagingDirectory);

        File.Exists(Path.Combine(paths.RuntimeDirectory, "host", "nvngx.dll")).Should().BeTrue();
        Directory.Exists(stagingDirectory).Should().BeFalse();
    }
}
