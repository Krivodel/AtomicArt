using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation.GoogleInteractions;

public sealed class GoogleInteractionsResponseParserTests
{
    [Fact]
    public void Parse_WithCompletedImageResponse_ReturnsImageContentAndUsage()
    {
        GoogleInteractionsResponseParser parser = new();
        string responseJson =
            GoogleInteractionsResponseJsonTestFactory.CreateCompletedImageResponse();

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
                    "modality": " TeXt ",
                    "tokens": 400
                  },
                  {
                    "modality": " IMAGE ",
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
        AssertInvalidUsageProperty("input_tokens_by_modality", breakdownJson);
    }

    [Theory]
    [InlineData("""[{ "tokens": 400 }]""")]
    [InlineData("""[{ "modality": "text" }]""")]
    [InlineData("""[{ "modality": " ", "tokens": 400 }]""")]
    [InlineData("""[{ "modality": "text", "tokens": "400" }]""")]
    [InlineData("""[{ "modality": "text", "tokens": -1 }]""")]
    public void Parse_WithMalformedInputModalityBreakdownItem_ThrowsProviderException(string breakdownJson)
    {
        AssertInvalidUsageProperty("input_tokens_by_modality", breakdownJson);
    }

    [Theory]
    [InlineData("""[{ "tokens": 500 }]""")]
    [InlineData("""[{ "modality": "text" }]""")]
    [InlineData("""[{ "modality": " ", "tokens": 500 }]""")]
    [InlineData("""[{ "modality": "text", "tokens": "500" }]""")]
    [InlineData("""[{ "modality": "text", "tokens": -1 }]""")]
    public void Parse_WithMalformedOutputModalityBreakdownItem_ThrowsProviderException(string breakdownJson)
    {
        AssertInvalidUsageProperty("output_tokens_by_modality", breakdownJson);
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
        AssertInvalidUsageProperty(propertyName, valueJson);
    }

    [Fact]
    public void Parse_WithInlineDataImageResponseAndMissingUsage_ThrowsProviderException()
    {
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

        Action act = CreateParseAction(responseJson);

        AssertInvalidResponseDoesNotExpose(act, "/9j/4AAQSkZJRg==");
    }

    [Fact]
    public void Parse_WithIncompleteUsage_ThrowsProviderException()
    {
        string responseJson = GoogleInteractionsResponseJsonTestFactory.CreateCompletedImageResponse(
            """
            "total_input_tokens": 1200,
            "total_tokens": 2320
            """);

        Action act = CreateParseAction(responseJson);

        AssertInvalidResponseDoesNotExpose(act, "2320");
    }

    [Fact]
    public void Parse_WithFailedStatus_ThrowsSafeProviderException()
    {
        string responseJson = """
            {
              "status": "failed",
              "error": {
                "message": "internal provider detail with secret"
              }
            }
            """;

        Action act = CreateParseAction(responseJson);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().NotContain("secret");
    }

    [Fact]
    public void Parse_WithoutImages_ThrowsSafeProviderException()
    {
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

        Action act = CreateParseAction(responseJson);

        GoogleInteractionsException exception = AssertInvalidResponseDoesNotExpose(act, "done");

        exception.NoImageDiagnostics.Should().BeEquivalentTo(new
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

        Action act = CreateParseAction(responseJson);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().NotContain("not-base64");
    }

    [Fact]
    public void Parse_WithPngImageOutput_ThrowsSafeProviderException()
    {
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

        Action act = CreateParseAction(responseJson);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().NotContain("iVBORw0KGgo=");
    }

    private static void AssertInvalidUsageProperty(
        string propertyName,
        string valueJson)
    {
        string responseJson =
            GoogleInteractionsResponseJsonTestFactory.CreateCompletedImageResponseWithAdditionalUsage(
            $"""
            ,
                "{propertyName}": {valueJson}
            """);

        Action act = CreateParseAction(responseJson);

        AssertInvalidUsage(act);
    }

    private static Action CreateParseAction(string responseJson)
    {
        GoogleInteractionsResponseParser parser = new();

        return () => parser.Parse(responseJson);
    }

    private static void AssertInvalidUsage(Action act)
    {
        GoogleInteractionsException exception = AssertInvalidResponseDoesNotExpose(
            act,
            "/9j/4AAQSkZJRg==");
        exception.Message.Should().NotContain("Prompt");
        exception.Message.Should().NotContain(TestGenerationCredentials.ProviderCredential);
    }

    private static GoogleInteractionsException AssertInvalidResponseDoesNotExpose(
        Action act,
        string sensitiveValue)
    {
        FluentAssertions.Specialized.ExceptionAssertions<GoogleInteractionsException> assertions = act.Should()
            .Throw<GoogleInteractionsException>();
        assertions.Which.FailureKind.Should().Be(ImageGenerationProviderFailureKind.InvalidResponse);
        assertions.Which.Message.Should().NotContain(sensitiveValue);

        return assertions.Which;
    }
}
