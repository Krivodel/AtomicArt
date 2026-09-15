using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Paths;
using static AtomicArt.Desktop.Tests.Common.DesktopTestDirectories;

namespace AtomicArt.Desktop.Tests.Services.Dlss5;

public sealed class Dlss5NativeEngineTests
{
    [Fact]
    public async Task InitializeAsync_WithStaleWorkerDirectories_DeletesOnlyCanonicalWorkers()
    {
        string rootDirectory = CreateCleanDirectory(
            nameof(InitializeAsync_WithStaleWorkerDirectories_DeletesOnlyCanonicalWorkers));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        Dlss5ModulePaths paths = new(pathProvider);
        Dlss5NativeEngineTests.CreateRequiredRuntime(paths);
        string staleWorkerDirectory = Path.Combine(
            paths.RuntimeDirectory,
            "dlss5-worker-11111111111111111111111111111111");
        string similarlyNamedDirectory = Path.Combine(paths.RuntimeDirectory, "dlss5-worker-manual");
        string benchmarkDirectory = Path.Combine(paths.RuntimeDirectory, "preparation-benchmark");
        Directory.CreateDirectory(Path.Combine(staleWorkerDirectory, "host"));
        Directory.CreateDirectory(similarlyNamedDirectory);
        Directory.CreateDirectory(benchmarkDirectory);
        File.WriteAllText(Path.Combine(staleWorkerDirectory, "host", "stale.bin"), "stale");
        using Dlss5NativeEngine engine = new(paths, NullLogger<Dlss5NativeEngine>.Instance);

        await engine.InitializeAsync(progress: null, CancellationToken.None);

        Directory.Exists(staleWorkerDirectory).Should().BeFalse();
        Directory.Exists(similarlyNamedDirectory).Should().BeTrue();
        Directory.Exists(benchmarkDirectory).Should().BeTrue();
        File.Exists(paths.WorkerPath).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithCurrentProcessOwnedWorkerDirectory_PreservesWorkerDirectory()
    {
        string rootDirectory = CreateCleanDirectory(
            nameof(InitializeAsync_WithCurrentProcessOwnedWorkerDirectory_PreservesWorkerDirectory));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        Dlss5ModulePaths paths = new(pathProvider);
        Dlss5NativeEngineTests.CreateRequiredRuntime(paths);
        string workerDirectory = Path.Combine(
            paths.RuntimeDirectory,
            "dlss5-worker-22222222222222222222222222222222");
        Directory.CreateDirectory(Path.Combine(workerDirectory, "host"));
        Dlss5WorkerDirectoryLifecycle.MarkOwnedByCurrentProcess(workerDirectory);
        using Dlss5NativeEngine engine = new(paths, NullLogger<Dlss5NativeEngine>.Instance);

        await engine.InitializeAsync(progress: null, CancellationToken.None);

        Directory.Exists(workerDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenAlreadyInitialized_DeletesNewStaleWorkerDirectory()
    {
        string rootDirectory = CreateCleanDirectory(
            nameof(InitializeAsync_WhenAlreadyInitialized_DeletesNewStaleWorkerDirectory));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        Dlss5ModulePaths paths = new(pathProvider);
        Dlss5NativeEngineTests.CreateRequiredRuntime(paths);
        using Dlss5NativeEngine engine = new(paths, NullLogger<Dlss5NativeEngine>.Instance);
        await engine.InitializeAsync(progress: null, CancellationToken.None);
        string staleWorkerDirectory = Path.Combine(
            paths.RuntimeDirectory,
            "dlss5-worker-33333333333333333333333333333333");
        Directory.CreateDirectory(Path.Combine(staleWorkerDirectory, "host"));

        await engine.InitializeAsync(progress: null, CancellationToken.None);

        Directory.Exists(staleWorkerDirectory).Should().BeFalse();
    }

    private static void CreateRequiredRuntime(Dlss5ModulePaths paths)
    {
        string[] requiredFiles =
        [
            paths.WorkerPath,
            paths.HostDxgiPath,
            paths.AddonPath,
            paths.NeuralRuntimePath,
            paths.SuperResolutionPath
        ];

        foreach (string path in requiredFiles)
        {
            string directory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException($"Unable to resolve the directory for '{path}'.");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, [1]);
        }
    }
}
