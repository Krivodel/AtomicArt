using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;

namespace AtomicArt.Infrastructure.Tests.Generation.GoogleInteractions;

public sealed class GoogleInteractionsResponseParserTests
{
    [Fact]
    public void Parse_WithCompletedImageResponse_ReturnsImageContentAndUsage()
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = """
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                    {
                      "type": "image",
                      "mime_type": "image/jpeg",
                      "data": "/9j/4AAQSkZJRg=="
                    }
                  ]
                }
              ],
              "usage": {
                "total_input_tokens": 1200,
                "total_output_tokens": 1120,
                "total_tokens": 2320
              }
            }
            """;

        GoogleInteractionsResult result = parser.Parse(responseJson);
        GenerationUsageDto usage = result.Usage
            ?? throw new InvalidOperationException("Usage is required.");

        result.Images.Should().ContainSingle();
        result.Images[0].ContentType.Should().Be("image/jpeg");
        result.Images[0].Base64Data.Should().Be("/9j/4AAQSkZJRg==");
        usage.InputTokensByModality.Should().BeNull();
        usage.OutputTokensByModality.Should().BeNull();
        usage.TotalThoughtTokens.Should().BeNull();
        usage.TotalToolUseTokens.Should().BeNull();
        usage.TotalCachedTokens.Should().BeNull();
        usage.Should().BeEquivalentTo(new
        {
            TotalInputTokens = 1200,
            TotalOutputTokens = 1120,
            TotalTokens = 2320
        });
    }

    [Fact]
    public void Parse_WithUsageModalityBreakdown_ReturnsFullUsage()
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = """
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                    {
                      "type": "image",
                      "mime_type": "image/jpeg",
                      "data": "/9j/4AAQSkZJRg=="
                    }
                  ]
                }
              ],
              "usage": {
                "total_input_tokens": 1000,
                "total_output_tokens": 2500,
                "total_thought_tokens": 250,
                "total_tool_use_tokens": 50,
                "total_cached_tokens": 100,
                "total_tokens": 3800,
                "input_tokens_by_modality": [
                  {
                    "modality": "text",
                    "tokens": 400
                  },
                  {
                    "modality": "image",
                    "tokens": 600
                  }
                ],
                "output_tokens_by_modality": [
                  {
                    "modality": "image",
                    "token_count": 2000
                  },
                  {
                    "modality": "text",
                    "tokens": 500
                  }
                ]
              }
            }
            """;

        GoogleInteractionsResult result = parser.Parse(responseJson);

        result.Usage.Should().BeEquivalentTo(new
        {
            TotalInputTokens = 1000,
            TotalOutputTokens = 2500,
            TotalThoughtTokens = 250,
            TotalToolUseTokens = 50,
            TotalCachedTokens = 100,
            TotalTokens = 3800,
            InputTokensByModality = new[]
            {
                new
                {
                    Modality = GenerationUsageModalityNames.Text,
                    Tokens = 400
                },
                new
                {
                    Modality = GenerationUsageModalityNames.Image,
                    Tokens = 600
                }
            },
            OutputTokensByModality = new[]
            {
                new
                {
                    Modality = GenerationUsageModalityNames.Image,
                    Tokens = 2000
                },
                new
                {
                    Modality = GenerationUsageModalityNames.Text,
                    Tokens = 500
                }
            }
        });
    }

    [Theory]
    [InlineData("\"not-array\"")]
    [InlineData("{}")]
    public void Parse_WithPresentNonArrayModalityBreakdown_ThrowsProviderException(string breakdownJson)
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = CreateCompletedImageResponseJson(
            $"""
            ,
                "input_tokens_by_modality": {breakdownJson}
            """);

        Action act = () => parser.Parse(responseJson);

        AssertInvalidUsage(act);
    }

    [Theory]
    [InlineData("""[{ "tokens": 400 }]""")]
    [InlineData("""[{ "modality": "text" }]""")]
    [InlineData("""[{ "modality": " ", "tokens": 400 }]""")]
    [InlineData("""[{ "modality": "text", "tokens": "400" }]""")]
    [InlineData("""[{ "modality": "text", "tokens": -1 }]""")]
    public void Parse_WithMalformedInputModalityBreakdownItem_ThrowsProviderException(string breakdownJson)
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = CreateCompletedImageResponseJson(
            $"""
            ,
                "input_tokens_by_modality": {breakdownJson}
            """);

        Action act = () => parser.Parse(responseJson);

        AssertInvalidUsage(act);
    }

    [Theory]
    [InlineData("""[{ "tokens": 500 }]""")]
    [InlineData("""[{ "modality": "text" }]""")]
    [InlineData("""[{ "modality": " ", "tokens": 500 }]""")]
    [InlineData("""[{ "modality": "text", "tokens": "500" }]""")]
    [InlineData("""[{ "modality": "text", "tokens": -1 }]""")]
    public void Parse_WithMalformedOutputModalityBreakdownItem_ThrowsProviderException(string breakdownJson)
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = CreateCompletedImageResponseJson(
            $"""
            ,
                "output_tokens_by_modality": {breakdownJson}
            """);

        Action act = () => parser.Parse(responseJson);

        AssertInvalidUsage(act);
    }

    [Theory]
    [InlineData("total_thought_tokens", "\"250\"")]
    [InlineData("total_thought_tokens", "-1")]
    [InlineData("total_tool_use_tokens", "\"50\"")]
    [InlineData("total_tool_use_tokens", "-1")]
    [InlineData("total_cached_tokens", "\"100\"")]
    [InlineData("total_cached_tokens", "-1")]
    public void Parse_WithInvalidOptionalTokenCounter_ThrowsProviderException(
        string propertyName,
        string valueJson)
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = CreateCompletedImageResponseJson(
            $"""
            ,
                "{propertyName}": {valueJson}
            """);

        Action act = () => parser.Parse(responseJson);

        AssertInvalidUsage(act);
    }

    [Fact]
    public void Parse_WithInlineDataImageResponseAndMissingUsage_ThrowsProviderException()
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = """
            {
              "state": "succeeded",
              "steps": [
                {
                  "content": [
                    {
                      "inlineData": {
                        "mimeType": "image/jpeg",
                        "data": "/9j/4AAQSkZJRg=="
                      }
                    }
                  ]
                }
              ]
            }
            """;

        Action act = () => parser.Parse(responseJson);

        FluentAssertions.Specialized.ExceptionAssertions<GoogleInteractionsException> assertions = act.Should()
            .Throw<GoogleInteractionsException>();
        assertions.Which.FailureKind.Should().Be(ImageGenerationProviderFailureKind.InvalidResponse);
        assertions.Which.Message.Should().NotContain("/9j/4AAQSkZJRg==");
    }

    [Fact]
    public void Parse_WithIncompleteUsage_ThrowsProviderException()
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = """
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                    {
                      "type": "image",
                      "mime_type": "image/jpeg",
                      "data": "/9j/4AAQSkZJRg=="
                    }
                  ]
                }
              ],
              "usage": {
                "total_input_tokens": 1200,
                "total_tokens": 2320
              }
            }
            """;

        Action act = () => parser.Parse(responseJson);

        FluentAssertions.Specialized.ExceptionAssertions<GoogleInteractionsException> assertions = act.Should()
            .Throw<GoogleInteractionsException>();
        assertions.Which.FailureKind.Should().Be(ImageGenerationProviderFailureKind.InvalidResponse);
        assertions.Which.Message.Should().NotContain("2320");
    }

    [Fact]
    public void Parse_WithFailedStatus_ThrowsSafeProviderException()
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = """
            {
              "status": "failed",
              "error": {
                "message": "internal provider detail with secret"
              }
            }
            """;

        Action act = () => parser.Parse(responseJson);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().NotContain("secret");
    }

    [Fact]
    public void Parse_WithoutImages_ThrowsSafeProviderException()
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = """
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                    {
                      "type": "text",
                      "text": "done"
                    }
                  ]
                }
              ]
            }
            """;

        Action act = () => parser.Parse(responseJson);

        FluentAssertions.Specialized.ExceptionAssertions<GoogleInteractionsException> assertions = act.Should()
            .Throw<GoogleInteractionsException>();
        assertions.Which.Message.Should().NotContain("done");
        assertions.Which.FailureKind.Should().Be(ImageGenerationProviderFailureKind.InvalidResponse);
        assertions.Which.NoImageDiagnostics.Should().BeEquivalentTo(new
        {
            Category = "text_only",
            Status = "completed",
            HasOutputImage = false,
            HasOutput = false,
            HasOutputImages = false,
            HasStepsTextContent = true,
            HasModelOutputTextContent = false,
            HasContentTextContent = true,
            TextContentLength = 4,
            TextContentItemCount = 1
        });
    }

    [Fact]
    public void Parse_WithInvalidImageBase64_ThrowsSafeProviderException()
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = """
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                    {
                      "type": "image",
                      "mime_type": "image/jpeg",
                      "data": "not-base64!"
                    }
                  ]
                }
              ]
            }
            """;

        Action act = () => parser.Parse(responseJson);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().NotContain("not-base64");
    }

    [Fact]
    public void Parse_WithPngImageOutput_ThrowsSafeProviderException()
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson = """
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                    {
                      "type": "image",
                      "mime_type": "image/png",
                      "data": "iVBORw0KGgo="
                    }
                  ]
                }
              ]
            }
            """;

        Action act = () => parser.Parse(responseJson);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().NotContain("iVBORw0KGgo=");
    }

    private static string CreateCompletedImageResponseJson(string usageProperties)
    {
        return $$"""
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                    {
                      "type": "image",
                      "mime_type": "image/jpeg",
                      "data": "/9j/4AAQSkZJRg=="
                    }
                  ]
                }
              ],
              "usage": {
                "total_input_tokens": 1200,
                "total_output_tokens": 1120,
                "total_tokens": 2320{{usageProperties}}
              }
            }
            """;
    }

    private static void AssertInvalidUsage(Action act)
    {
        FluentAssertions.Specialized.ExceptionAssertions<GoogleInteractionsException> assertions = act.Should()
            .Throw<GoogleInteractionsException>();
        assertions.Which.FailureKind.Should().Be(ImageGenerationProviderFailureKind.InvalidResponse);
        assertions.Which.Message.Should().NotContain("/9j/4AAQSkZJRg==");
        assertions.Which.Message.Should().NotContain("Prompt");
        assertions.Which.Message.Should().NotContain("test-provider-key");
    }
}
