using System.Text;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.OpenRouter;

namespace AtomicArt.Infrastructure.Tests.Generation.OpenRouter;

public sealed class OpenRouterImageUsageReaderTests
{
    [Fact]
    public void ReadPrice_WithLargeImageResponse_ReturnsProviderReportedCost()
    {
        string response = "{\"created\":1,\"data\":[{\"b64_json\":\""
            + new string('A', 100000)
            + "\",\"media_type\":\"image/png\"}],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":34,\"total_tokens\":46,\"cost\":0.0123}}";
        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        OpenRouterImageUsageReader reader = new();

        for (int offset = 0; offset < responseBytes.Length; offset += 127)
        {
            int count = Math.Min(127, responseBytes.Length - offset);
            reader.Append(responseBytes.AsSpan(offset, count));
        }

        GenerationPriceDto? price = reader.ReadPrice();

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.0123m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }
}
