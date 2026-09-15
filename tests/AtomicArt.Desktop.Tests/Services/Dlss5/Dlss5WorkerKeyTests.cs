using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Dlss5;

namespace AtomicArt.Desktop.Tests.Services.Dlss5;

public sealed class Dlss5WorkerKeyTests
{
    [Fact]
    public void Matches_WhenDimensionsAndAllWorkerSettingsAreEqual_ReturnsTrue()
    {
        Dlss5RenderSettings settings = Dlss5RenderSettings.Default;
        Dlss5WorkerKey key = new(1024, 768, settings);

        key.Matches(1024, 768, settings).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenAnyRestartInputChanges_ReturnsFalse()
    {
        Dlss5RenderSettings settings = Dlss5RenderSettings.Default;
        Dlss5WorkerKey key = new(1024, 768, settings);

        key.Matches(1023, 768, settings).Should().BeFalse();
        key.Matches(1024, 767, settings).Should().BeFalse();
        key.Matches(1024, 768, settings with { Style = Dlss5Style.Natural }).Should().BeFalse();
        key.Matches(1024, 768, settings with { GeneralIntensity = 1f }).Should().BeFalse();
        key.Matches(1024, 768, settings with { LocalStructureIntensity = 1f }).Should().BeFalse();
        key.Matches(1024, 768, settings with { SkinStructureStrength = 0f }).Should().BeFalse();
        key.Matches(1024, 768, settings with { LocalToneStrength = 0f }).Should().BeFalse();
    }

    [Fact]
    public void NormalizeForNative_CollapsesSliderFloatNoise()
    {
        Dlss5RenderSettings noisy = Dlss5RenderSettings.Default with
        {
            GeneralIntensity = 1.0000001f,
            LocalStructureIntensity = 1.234567f,
            SkinStructureStrength = -0.9999999f,
            LocalToneStrength = 0.50000006f
        };

        noisy.NormalizeForNative().Should().Be(new Dlss5RenderSettings(
            Dlss5Style.Cinematic,
            1f,
            1.2346f,
            -1f,
            0.5f));
    }

    [Fact]
    public void Matches_TreatsEquivalentSliderFloatNoiseAsTheSameWorkerProfile()
    {
        Dlss5WorkerKey key = new(1024, 768, Dlss5RenderSettings.Default);
        Dlss5RenderSettings noisy = Dlss5RenderSettings.Default with
        {
            GeneralIntensity = 1.9999999f,
            LocalStructureIntensity = 2.0000000f,
            SkinStructureStrength = -1.0000000f,
            LocalToneStrength = 0.99999994f
        };

        key.Matches(1024, 768, noisy).Should().BeTrue();
    }
}
