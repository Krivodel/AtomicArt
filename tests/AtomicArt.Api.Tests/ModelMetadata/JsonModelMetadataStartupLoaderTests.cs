using System.Text;

using FluentAssertions;
using Xunit;

using AtomicArt.Api.ModelMetadata;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Api.Tests.ModelMetadata;

public sealed class JsonModelMetadataStartupLoaderTests
{
    [Fact]
    public void Load_WithRealMetadata_HasAutoAspectRatioFirst()
    {
        GenerationModelCatalogDto catalog = ApiModelMetadataStartupTestCatalog.LoadCatalog();

        catalog.Models.Should().HaveCount(3);
        catalog.Models.Should().OnlyContain(model =>
            model.AspectRatios.First() == GenerationAspectRatios.Auto);
        catalog.Models.Should().OnlyContain(model =>
            model.AspectRatios.Contains(GenerationAspectRatios.Auto, StringComparer.Ordinal));
    }

    [Fact]
    public void Load_WithRealMetadata_HasTemperatureForAllNanoBananaModels()
    {
        GenerationModelCatalogDto catalog = ApiModelMetadataStartupTestCatalog.LoadCatalog();

        catalog.Models.Should().HaveCount(3);
        catalog.Models.Should().OnlyContain(model => model.Temperature.Minimum == 0.1d);
        catalog.Models.Should().OnlyContain(model => model.Temperature.Maximum == 2d);
        catalog.Models.Should().OnlyContain(model => model.Temperature.Default == 1d);
        catalog.Models.Should().OnlyContain(model => model.Temperature.Step == 0.1d);
    }

    [Fact]
    public void Load_WithRealMetadata_HasThinkingOnlyForSupportedNanoBananaModels()
    {
        GenerationModelCatalogDto catalog = ApiModelMetadataStartupTestCatalog.LoadCatalog();

        GenerationModelMetadataDto nanoBanana2 = catalog.Models.Single(model => model.Id == "nano-banana-2");
        GenerationModelMetadataDto nanoBanana2Lite = catalog.Models.Single(model => model.Id == "nano-banana-2-lite");
        GenerationModelMetadataDto nanoBananaPro = catalog.Models.Single(model => model.Id == "nano-banana-pro");
        GenerationModelThinkingMetadataDto expectedThinking = new(
            [
                new("low", "Минимальный"),
                new("high", "Максимальный")
            ],
            "low");

        nanoBanana2.Thinking.Should().BeEquivalentTo(expectedThinking);
        nanoBanana2Lite.Thinking.Should().BeEquivalentTo(expectedThinking);
        nanoBananaPro.Thinking.Should().BeNull();
    }

    [Fact]
    public void Load_WithValidJson_ReturnsCatalogSnapshot()
    {
        string path = CreateTempFile(CreateValidJson());

        try
        {
            GenerationModelCatalogDto catalog = Load(path);

            catalog.Models.Should().ContainSingle();
            GenerationModelMetadataDto metadata = catalog.Models.Single();
            metadata.Id.Should().Be("test-model");
            metadata.Provider.Should().Be("google");
            metadata.ProviderModelId.Should().Be("provider-test-model");
            metadata.PanelId.Should().Be(GenerationPanelIds.NanoBanana);
            metadata.AspectRatios.Should().Equal("Авто", "1:1");
            metadata.Temperature.Should().Be(
                new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d));
            metadata.Attachments.SupportedContentTypes.Should().Equal(GenerationImageContentTypes.Png);
            metadata.Pricing.OutputImageTokensByResolution["1k"].Should().Be(1120);
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithMissingFile_ThrowsInvalidOperationException()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.Api.Tests",
            $"missing-{GenerationModelCatalogDefaults.FileName}");

        Action action = () => JsonModelMetadataStartupLoader.Load(path, new ThrowingGenerationModelCatalogJsonSource());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void Load_WithEmptyJson_ThrowsInvalidOperationException()
    {
        string path = CreateTempFile(string.Empty);

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*empty*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithInvalidJson_ThrowsInvalidOperationException()
    {
        string path = CreateTempFile("{");

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*malformed JSON*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithEmptyCatalog_ThrowsInvalidOperationException()
    {
        string path = CreateTempFile(
            """
            {
              "models": []
            }
            """);

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*empty catalog*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithDuplicateModelIds_ThrowsInvalidOperationException()
    {
        string path = CreateTempFile(
            """
            {
              "models": [
                {
                  "id": "duplicate",
                  "displayName": "Duplicate",
                  "provider": "google",
                  "providerModelId": "provider-duplicate",
                  "panelId": "nano-banana",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "aspectRatios": [ "авто" ],
                  "resolutions": [ "1k" ],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": 1024,
                    "supportedContentTypes": [ "image/png" ]
                  },
                  "pricing": {
                    "currencyCode": "USD",
                    "inputTokenUsdPerMillion": 0.25,
                    "textOutputTokenUsdPerMillion": 1.50,
                    "imageOutputTokenUsdPerMillion": 30.00,
                    "inputImageTokens": 1120,
                    "outputImageTokensByResolution": { "1k": 1120 }
                  }
                },
                {
                  "id": "duplicate",
                  "displayName": "Duplicate 2",
                  "provider": "google",
                  "providerModelId": "provider-duplicate-2",
                  "panelId": "nano-banana",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "aspectRatios": [ "авто" ],
                  "resolutions": [ "1k" ],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": 1024,
                    "supportedContentTypes": [ "image/png" ]
                  },
                  "pricing": {
                    "currencyCode": "USD",
                    "inputTokenUsdPerMillion": 0.25,
                    "textOutputTokenUsdPerMillion": 1.50,
                    "imageOutputTokenUsdPerMillion": 30.00,
                    "inputImageTokens": 1120,
                    "outputImageTokensByResolution": { "1k": 1120 }
                  }
                }
              ]
            }
            """);

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*duplicate model identifier*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithMissingRequiredList_ThrowsInvalidOperationException()
    {
        string path = CreateTempFile(
            """
            {
              "models": [
                {
                  "id": "test-model",
                  "displayName": "Test Model",
                  "provider": "google",
                  "providerModelId": "provider-test-model",
                  "panelId": "nano-banana",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "resolutions": [ "1k" ],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": 1024,
                    "supportedContentTypes": [ "image/png" ]
                  },
                  "pricing": {
                    "currencyCode": "USD",
                    "inputTokenUsdPerMillion": 0.25,
                    "textOutputTokenUsdPerMillion": 1.50,
                    "imageOutputTokenUsdPerMillion": 30.00,
                    "inputImageTokens": 1120,
                    "outputImageTokensByResolution": { "1k": 1120 }
                  }
                }
              ]
            }
            """);

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*aspectRatios*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithEmptyResolutions_ThrowsInvalidOperationException()
    {
        string path = CreateTempFile(
            """
            {
              "models": [
                {
                  "id": "test-model",
                  "displayName": "Test Model",
                  "provider": "google",
                  "providerModelId": "provider-test-model",
                  "panelId": "nano-banana",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "aspectRatios": [ "авто" ],
                  "resolutions": [],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": 1024,
                    "supportedContentTypes": [ "image/png" ]
                  },
                  "pricing": {
                    "currencyCode": "USD",
                    "inputTokenUsdPerMillion": 0.25,
                    "textOutputTokenUsdPerMillion": 1.50,
                    "imageOutputTokenUsdPerMillion": 30.00,
                    "inputImageTokens": 1120,
                    "outputImageTokensByResolution": { "1k": 1120 }
                  }
                }
              ]
            }
            """);

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*resolutions*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithMissingPricing_ThrowsInvalidOperationException()
    {
        string path = CreateTempFile(
            """
            {
              "models": [
                {
                  "id": "test-model",
                  "displayName": "Test Model",
                  "provider": "google",
                  "providerModelId": "provider-test-model",
                  "panelId": "nano-banana",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "aspectRatios": [ "авто" ],
                  "resolutions": [ "1k" ],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": 2048,
                    "supportedContentTypes": [ "image/png" ]
                  }
                }
              ]
            }
            """);

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*pricing*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithMissingProvider_ThrowsInvalidOperationExceptionWithModelName()
    {
        string path = CreateTempFile(
            """
            {
              "models": [
                {
                  "id": "test-model",
                  "displayName": "Test Model",
                  "providerModelId": "provider-test-model",
                  "panelId": "nano-banana",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "aspectRatios": [ "авто" ],
                  "resolutions": [ "1k" ],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": 2048,
                    "supportedContentTypes": [ "image/png" ]
                  },
                  "pricing": {
                    "currencyCode": "USD",
                    "inputTokenUsdPerMillion": 0.25,
                    "textOutputTokenUsdPerMillion": 1.50,
                    "imageOutputTokenUsdPerMillion": 30.00,
                    "inputImageTokens": 1120,
                    "outputImageTokensByResolution": { "1k": 1120 }
                  }
                }
              ]
            }
            """);

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*Test Model*test-model*provider*")
                .And.Message.Should().NotContain("index");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithMissingPanelId_ThrowsInvalidOperationExceptionWithModelName()
    {
        string path = CreateTempFile(
            """
            {
              "models": [
                {
                  "id": "test-model",
                  "displayName": "Test Model",
                  "provider": "google",
                  "providerModelId": "provider-test-model",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "aspectRatios": [ "авто" ],
                  "resolutions": [ "1k" ],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": 2048,
                    "supportedContentTypes": [ "image/png" ]
                  },
                  "pricing": {
                    "currencyCode": "USD",
                    "inputTokenUsdPerMillion": 0.25,
                    "textOutputTokenUsdPerMillion": 1.50,
                    "imageOutputTokenUsdPerMillion": 30.00,
                    "inputImageTokens": 1120,
                    "outputImageTokensByResolution": { "1k": 1120 }
                  }
                }
              ]
            }
            """);

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*Test Model*test-model*panelId*")
                .And.Message.Should().NotContain("index");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    private static string CreateValidJson()
    {
        return
            """
            {
              "models": [
                {
                  "id": "test-model",
                  "displayName": "Test Model",
                  "provider": "google",
                  "providerModelId": "provider-test-model",
                  "panelId": "nano-banana",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "aspectRatios": [ "Авто", "1:1" ],
                  "resolutions": [ "1k" ],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": 2048,
                    "supportedContentTypes": [ "image/png" ]
                  },
                  "pricing": {
                    "currencyCode": "USD",
                    "inputTokenUsdPerMillion": 0.25,
                    "textOutputTokenUsdPerMillion": 1.50,
                    "imageOutputTokenUsdPerMillion": 30.00,
                    "inputImageTokens": 1120,
                    "outputImageTokensByResolution": {
                      "1k": 1120
                    }
                  }
                }
              ]
            }
            """;
    }

    private static string CreateTempFile(string content)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.Api.Tests",
            nameof(JsonModelMetadataStartupLoaderTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        string path = Path.Combine(directoryPath, GenerationModelCatalogDefaults.FileName);
        File.WriteAllText(path, content, Encoding.UTF8);

        return path;
    }

    private static GenerationModelCatalogDto Load(string path)
    {
        string json = File.ReadAllText(path, Encoding.UTF8);

        return JsonModelMetadataStartupLoader.Load(
            path,
            new FixedGenerationModelCatalogJsonSource(json));
    }

    private static void DeleteFileDirectory(string path)
    {
        string? directoryPath = Path.GetDirectoryName(path);

        if (directoryPath is not null && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }
    }

    private sealed class ThrowingGenerationModelCatalogJsonSource : IGenerationModelCatalogJsonSource
    {
        public string Read(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            throw new InvalidOperationException(
                $"Generation model metadata file '{path}' was not found.");
        }
    }
}
