using System.Buffers.Binary;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Dlss5;
using static AtomicArt.Desktop.Tests.Common.DesktopTestDirectories;

namespace AtomicArt.Desktop.Tests.Services.Dlss5;

public sealed class Dlss5WorkerProtocolTests
{
    [Fact]
    public void CreateVideoHeader_WithV7Settings_UsesCanonicalFieldOrderAndFixedDefaults()
    {
        Dlss5RenderSettings settings = new(
            Dlss5Style.Cinematic,
            1.75f,
            0.5f,
            -0.25f,
            0.25f);

        byte[] header = Dlss5NativeEngine.CreateVideoHeader(128, 96, settings, streaming: true);

        header.Should().HaveCount(72);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 0).Should().Be(0x3456_3544u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 4).Should().Be(128u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 8).Should().Be(96u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 12).Should().Be(128u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 16).Should().Be(96u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 20).Should().Be(0u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 24).Should().Be(0u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 28).Should().Be(5u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 32).Should().Be(0u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 36).Should().Be(0u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 40).Should().Be(0u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 44).Should().Be((uint)Dlss5Style.Cinematic);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 48).Should().Be(0u);
        Dlss5WorkerProtocolTests.ReadUInt32(header, 52).Should().Be(0u);
        Dlss5WorkerProtocolTests.ReadSingle(header, 56).Should().Be(1.75f);
        Dlss5WorkerProtocolTests.ReadSingle(header, 60).Should().Be(0.25f);
        Dlss5WorkerProtocolTests.ReadSingle(header, 64).Should().Be(0.5f);
        Dlss5WorkerProtocolTests.ReadSingle(header, 68).Should().Be(-0.25f);
    }

    [Fact]
    public void CreateVideoHeader_WithOneShotMode_SetsOneShotFrameCountOnly()
    {
        byte[] header = Dlss5NativeEngine.CreateVideoHeader(
            64,
            64,
            Dlss5RenderSettings.Default,
            streaming: false);

        Dlss5WorkerProtocolTests.ReadUInt32(header, 24).Should().Be(1u);
    }

    [Fact]
    public void CreateReShadeProfile_WithIsolatedAddonPath_UsesThatPathWithoutChangingV7Controls()
    {
        const string AddonPath = "C:\\AtomicArt\\Modules\\DLSS 5\\runtime\\dlssnr";

        string profile = Dlss5NativeEngine.CreateReShadeProfile(
            Dlss5RenderSettings.Default,
            AddonPath);

        profile.Should().Contain("EnableHooks=2");
        profile.Should().Contain("NRStyle=2");
        profile.Should().Contain("NRAutoMask=0");
        profile.Should().Contain("NREnableUpscaling=0");
        profile.Should().Contain($"AddonPath={AddonPath}");
        profile.Should().NotContain("AddonPath=..\\dlssnr");
    }

    [Fact]
    public void CreateDynamicParameterContent_UsesV7OrderAndFixedGlobalTone()
    {
        Dlss5RenderSettings settings = new(
            Dlss5Style.Natural,
            1.75f,
            0.5f,
            -0.25f,
            0.25f);

        string content = Dlss5NativeEngine.CreateDynamicParameterContent(settings);

        content.Should().Be("1 1.75 1 0.25 0.5 -0.25\n");
    }

    [Fact]
    public void CreateWorkerAddonPath_WithHostWorkerDirectory_UsesRelativePathToRuntimeAddon()
    {
        const string WorkerDirectory =
            "F:\\Program Files\\Krivodeling\\Atomic Art Data\\Modules\\DLSS 5\\runtime\\host\\worker";
        const string AddonDirectory =
            "F:\\Program Files\\Krivodeling\\Atomic Art Data\\Modules\\DLSS 5\\runtime\\dlssnr";

        string addonPath = Dlss5NativeEngine.CreateWorkerAddonPath(
            WorkerDirectory,
            AddonDirectory);

        addonPath.Should().Be("..\\..\\dlssnr");
    }

    [Fact]
    public void TryOptimizeWorkerStartup_WithV7WorkerLoop_ReducesOnlyHookArmingIterations()
    {
        string directory = CreateCleanDirectory(
            nameof(TryOptimizeWorkerStartup_WithV7WorkerLoop_ReducesOnlyHookArmingIterations));
        string workerPath = Path.Combine(directory, "nvngx.dll");
        byte[] worker = new byte[0x3BDC];
        worker[0x3BD6] = 0xBB;
        worker[0x3BD7] = 120;
        worker[0x3BDB] = 0xBE;
        File.WriteAllBytes(workerPath, worker);

        bool optimized = Dlss5NativeEngine.TryOptimizeWorkerStartup(workerPath);

        byte[] result = File.ReadAllBytes(workerPath);
        optimized.Should().BeTrue();
        result[0x3BD6].Should().Be(0xBB);
        result[0x3BD7].Should().Be(20);
        result[0x3BDB].Should().Be(0xBE);
        result.Where((value, index) => (value != 0) && (index is not 0x3BD6 and not 0x3BD7 and not 0x3BDB))
            .Should().BeEmpty();
    }

    [Fact]
    public void TryOptimizeWorkerStartup_WithUnknownWorker_DoesNotModifyFile()
    {
        string directory = CreateCleanDirectory(
            nameof(TryOptimizeWorkerStartup_WithUnknownWorker_DoesNotModifyFile));
        string workerPath = Path.Combine(directory, "nvngx.dll");
        byte[] worker = [1, 2, 3, 4];
        File.WriteAllBytes(workerPath, worker);

        bool optimized = Dlss5NativeEngine.TryOptimizeWorkerStartup(workerPath);

        optimized.Should().BeFalse();
        File.ReadAllBytes(workerPath).Should().Equal(worker);
    }

    private static uint ReadUInt32(byte[] header, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(offset, sizeof(uint)));
    }

    private static float ReadSingle(byte[] header, int offset)
    {
        return BitConverter.Int32BitsToSingle((int)Dlss5WorkerProtocolTests.ReadUInt32(header, offset));
    }
}
