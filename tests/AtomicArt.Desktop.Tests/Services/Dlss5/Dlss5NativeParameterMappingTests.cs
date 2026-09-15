using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Dlss5;

namespace AtomicArt.Desktop.Tests.Services.Dlss5;

public sealed class Dlss5NativeParameterMappingTests
{
    [Theory]
    [InlineData(Dlss5Style.Default)]
    [InlineData(Dlss5Style.Natural)]
    [InlineData(Dlss5Style.Cinematic)]
    public void Create_WithEachV7Style_PreservesProtocolValue(Dlss5Style style)
    {
        Dlss5RenderSettings settings = Dlss5RenderSettings.Default with
        {
            Style = style
        };

        Dlss5NativeParameterMapping mapped = Dlss5NativeParameterMapping.Create(settings);

        mapped.Style.Should().Be((int)style);
    }

    [Fact]
    public void Create_WithV7Settings_PreservesVisibleValuesAndFixedDefaults()
    {
        Dlss5RenderSettings settings = new(
            Dlss5Style.Natural,
            1.75f,
            0.5f,
            1.25f,
            0.25f);

        Dlss5NativeParameterMapping mapped = Dlss5NativeParameterMapping.Create(settings);

        mapped.Style.Should().Be((int)Dlss5Style.Natural);
        mapped.GeneralIntensity.Should().Be(1.75f);
        mapped.LocalStructureStrength.Should().Be(0.5f);
        mapped.SkinStructureStrength.Should().Be(1.25f);
        mapped.LocalToneStrength.Should().Be(0.25f);
        mapped.AutomaticMask.Should().Be(Dlss5NativeParameterMapping.AutomaticMaskOff);
    }

    [Fact]
    public void Create_WithVisibleControlBoundaries_PreservesRawV7Scale()
    {
        Dlss5RenderSettings[] settings =
        [
            Dlss5RenderSettings.Default with { GeneralIntensity = Dlss5RenderSettings.GeneralIntensityMinimum },
            Dlss5RenderSettings.Default with { GeneralIntensity = Dlss5RenderSettings.GeneralIntensityMaximum },
            Dlss5RenderSettings.Default with { LocalStructureIntensity = Dlss5RenderSettings.LocalStructureIntensityMinimum },
            Dlss5RenderSettings.Default with { LocalStructureIntensity = Dlss5RenderSettings.LocalStructureIntensityMaximum },
            Dlss5RenderSettings.Default with { SkinStructureStrength = Dlss5RenderSettings.SkinStructureStrengthMinimum },
            Dlss5RenderSettings.Default with { SkinStructureStrength = Dlss5RenderSettings.SkinStructureStrengthMaximum },
            Dlss5RenderSettings.Default with { LocalToneStrength = Dlss5RenderSettings.LocalToneStrengthMinimum },
            Dlss5RenderSettings.Default with { LocalToneStrength = Dlss5RenderSettings.LocalToneStrengthMaximum }
        ];

        foreach (Dlss5RenderSettings current in settings)
        {
            Dlss5NativeParameterMapping mapped = Dlss5NativeParameterMapping.Create(current);

            mapped.GeneralIntensity.Should().Be(current.GeneralIntensity);
            mapped.LocalStructureStrength.Should().Be(current.LocalStructureIntensity);
            mapped.SkinStructureStrength.Should().Be(current.SkinStructureStrength);
            mapped.LocalToneStrength.Should().Be(current.LocalToneStrength);
        }
    }

    [Fact]
    public void ProtocolDefaults_KeepV7DlaaAndHiddenControlsFixed()
    {
        Dlss5NativeParameterMapping.DlaaPerformanceQuality.Should().Be(5u);
        Dlss5NativeParameterMapping.FixedDlssModelPreset.Should().Be(0u);
        Dlss5NativeParameterMapping.FixedNrPreset.Should().Be(0u);
        Dlss5NativeParameterMapping.FixedProfile.Should().Be(0u);
        Dlss5NativeParameterMapping.FixedUiCorrection.Should().Be(0u);
        Dlss5NativeParameterMapping.NoWarmupFrames.Should().Be(0u);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(2f)]
    public void Create_WithAnySkinStrength_UsesCanonicalAutomaticMask(float skinStrength)
    {
        Dlss5RenderSettings settings = Dlss5RenderSettings.Default with
        {
            SkinStructureStrength = skinStrength
        };

        Dlss5NativeParameterMapping mapped = Dlss5NativeParameterMapping.Create(settings);

        mapped.AutomaticMask.Should().Be(Dlss5NativeParameterMapping.AutomaticMaskOff);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void Create_WithGeneralIntensity_PreservesV7Scale(float intensity)
    {
        Dlss5RenderSettings settings = Dlss5RenderSettings.Default with
        {
            GeneralIntensity = intensity
        };

        Dlss5NativeParameterMapping mapped = Dlss5NativeParameterMapping.Create(settings);

        mapped.GeneralIntensity.Should().Be(intensity);
    }

    [Fact]
    public void Create_WithUnsupportedStyle_ThrowsBeforeNativeCall()
    {
        Dlss5RenderSettings settings = Dlss5RenderSettings.Default with
        {
            Style = (Dlss5Style)99
        };

        Action action = () => Dlss5NativeParameterMapping.Create(settings);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be(nameof(Dlss5RenderSettings.Style));
    }

    [Fact]
    public void CreateReShadeProfile_UsesCanonicalV7KeysAndRawControlValues()
    {
        Dlss5RenderSettings settings = new(
            Dlss5Style.Cinematic,
            1.75f,
            0.5f,
            1.25f,
            0.25f);

        string profile = Dlss5NativeEngine.CreateReShadeProfile(settings);

        profile.Should().Be(
            "[ADDON]\r\n"
            + "AddonPath=..\\dlssnr\r\n\r\n"
            + "[RenoDX.DLSS5]\r\n"
            + "EnableHooks=2\r\n"
            + "NREnableUpscaling=0\r\n"
            + "NRPreset=0\r\n"
            + "NRStyle=2\r\n"
            + "NRAutoMask=0\r\n"
            + "NRUICorrection=0\r\n"
            + "NRIntensity=1.7500\r\n"
            + "NRLocalTone=0.2500\r\n"
            + "NRLocalStructure=0.5000\r\n"
            + "NRSkinStructure=1.2500\r\n");
    }
}
