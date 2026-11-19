using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Generation.LocalCodex;

public sealed class LocalCodexInstallationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resolve_WithDesktopInstallation_FindsExecutableAndLoginHome(bool selectInstallFolder)
    {
        using TemporaryDirectory directory = new(typeof(LocalCodexInstallationTests),
            $"{nameof(Resolve_WithDesktopInstallation_FindsExecutableAndLoginHome)}-{selectInstallFolder}");
        string root = directory.DirectoryPath;
        string home = Directory.CreateDirectory(Path.Combine(root, "CODEX_HOME")).FullName;
        string resources = Directory.CreateDirectory(Path.Combine(root, "versions", "current", "resources")).FullName;
        string executable = CreateCodexExecutable(resources);
        LocalCodexOptions options = selectInstallFolder ? new LocalCodexOptions(true, root) : LocalCodexOptions.Default;
        string? environmentHome = selectInstallFolder ? null : home;

        (string resolvedExecutable, string resolvedHome) = LocalCodexInstallation.Resolve(
            options, environmentHome, searchPath: null, userProfile: root);

        resolvedExecutable.Should().Be(executable);
        resolvedHome.Should().Be(home);
    }

    [Fact]
    public void Resolve_WithSeparateLegacyPaths_PreservesExecutableAndLoginHome()
    {
        using TemporaryDirectory directory = new(typeof(LocalCodexInstallationTests),
            nameof(Resolve_WithSeparateLegacyPaths_PreservesExecutableAndLoginHome));
        string root = directory.DirectoryPath;
        string home = Directory.CreateDirectory(Path.Combine(root, "login")).FullName;
        string executable = CreateCodexExecutable(root);
        string savedValue = JsonSerializer.Serialize(new { Enabled = true, ExecutablePath = executable, HomeDirectory = home });
        LocalCodexSettings settings = new();
        settings.ApplySerializedValue(savedValue);

        (string resolvedExecutable, string resolvedHome) = LocalCodexInstallation.Resolve(
            settings.Current, environmentHome: null, searchPath: null, userProfile: root);

        settings.Current.Enabled.Should().BeTrue();
        resolvedExecutable.Should().Be(executable);
        resolvedHome.Should().Be(home);
    }

    private static string CreateCodexExecutable(string directory)
    {
        string path = Path.Combine(directory, OperatingSystem.IsWindows() ? "codex.exe" : "codex");
        File.WriteAllText(path, string.Empty);
        return path;
    }
}
