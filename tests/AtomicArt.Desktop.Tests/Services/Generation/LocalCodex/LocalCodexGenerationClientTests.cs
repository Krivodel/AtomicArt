using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Tests.TestDoubles;
using BuiltInLocalizationCatalog = Krivodeling.Localization.Avalonia.BuiltInLocalizationCatalog;

namespace AtomicArt.Desktop.Tests.Services.Generation.LocalCodex;

public sealed class LocalCodexGenerationClientTests
{
    [Theory]
    [InlineData(GenerationAspectRatios.Auto, true, "A blue circle")]
    [InlineData("21:9", false, "A blue circle\n\nAspect ratio: 21:9")]
    [InlineData("16:9", true, "A blue circle\n\nСоотношение сторон: 16:9")]
    public void FormatPrompt_WithSelectedAspectRatio_UsesCurrentLanguage(
        string aspectRatio, bool useRussian, string expectedPrompt)
    {
        BuiltInLocalizationCatalog catalog = BuiltInLocalizationCatalog.FromAssemblies(typeof(LocalizationService).Assembly);
        TestLocalizationTextProvider textProvider = new(useRussian ? catalog.Russian.Strings : catalog.English.Strings);
        ImageGenerationRequestDto request = new(
            LocalCodexImageModel.Id,
            "A blue circle",
            aspectRatio,
            LocalCodexImageModel.Automatic,
            LocalCodexImageModel.Option.Temperature.Default,
            1,
            Array.Empty<AttachedImageDto>());

        string prompt = LocalCodexGenerationClient.FormatPrompt(request, textProvider);

        prompt.Should().Be(expectedPrompt);
    }
}
