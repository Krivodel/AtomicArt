using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Tests.Generation;

internal static class GenerationUsageTestFactory
{
    public static GenerationUsageDto CreateNanoBananaImageUsage()
    {
        return new GenerationUsageDto(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2320,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ]);
    }
}
