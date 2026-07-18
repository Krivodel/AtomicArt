using System.Text;
using System.Text.Json.Nodes;

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
    public void Load_WithProfile_AppliesProfileAndModelOverride()
    {
        (JsonObject catalogJson, JsonObject modelJson) = CreateValidModelJson();
        JsonObject profileJson = new();
        profileJson["temperature"] = modelJson["temperature"]?.DeepClone();
        modelJson.Remove("temperature");
        modelJson["provider"] = "model-provider";
        modelJson["profiles"] = new JsonArray("shared");
        catalogJson["profiles"] = new JsonObject
        {
            ["shared"] = profileJson
        };
        string path = CreateTempFile(catalogJson.ToJsonString());

        try
        {
            GenerationModelCatalogDto catalog = Load(path);

            GenerationModelMetadataDto metadata = catalog.Models.Should().ContainSingle().Subject;
            metadata.Provider.Should().Be("model-provider");
            metadata.Temperature.Should().Be(
                new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d));
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithUnknownProfile_ThrowsInvalidOperationException()
    {
        (JsonObject catalogJson, JsonObject modelJson) = CreateValidModelJson();
        modelJson["profiles"] = new JsonArray("missing");
        catalogJson["profiles"] = new JsonObject();
        string path = CreateTempFile(catalogJson.ToJsonString());

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*unknown profile*");
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
        string path = CreateTempFile(CreateJsonWithDuplicateModelId());

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
        const string PropertyName = "aspectRatios";
        string path = CreateTempFile(CreateJsonWithoutModelProperty(PropertyName));

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage($"*{PropertyName}*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithEmptyResolutions_ThrowsInvalidOperationException()
    {
        const string PropertyName = "resolutions";
        string path = CreateTempFile(CreateJsonWithEmptyModelArray(PropertyName));

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage($"*{PropertyName}*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithMissingPricing_ThrowsInvalidOperationException()
    {
        const string PropertyName = "pricing";
        string path = CreateTempFile(CreateJsonWithoutModelProperty(PropertyName));

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage($"*{PropertyName}*");
        }
        finally
        {
            DeleteFileDirectory(path);
        }
    }

    [Fact]
    public void Load_WithMissingProvider_ThrowsInvalidOperationExceptionWithModelName()
    {
        const string PropertyName = "provider";
        string path = CreateTempFile(CreateJsonWithoutModelProperty(PropertyName));

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage($"*Test Model*test-model*{PropertyName}*")
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
        const string PropertyName = "panelId";
        string path = CreateTempFile(CreateJsonWithoutModelProperty(PropertyName));

        try
        {
            Action action = () => Load(path);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage($"*Test Model*test-model*{PropertyName}*")
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

    private static string CreateJsonWithoutModelProperty(string propertyName)
    {
        (JsonObject catalog, JsonObject model) = CreateValidModelJson();
        model.Remove(propertyName);

        return catalog.ToJsonString();
    }

    private static string CreateJsonWithEmptyModelArray(string propertyName)
    {
        (JsonObject catalog, JsonObject model) = CreateValidModelJson();
        model[propertyName] = new JsonArray();

        return catalog.ToJsonString();
    }

    private static string CreateJsonWithDuplicateModelId()
    {
        const string DuplicateModelId = "duplicate";
        (JsonObject catalog, JsonObject firstModel) = CreateValidModelJson();
        JsonObject secondModel = firstModel.DeepClone().AsObject();
        JsonArray models = GetModels(catalog);

        firstModel["id"] = DuplicateModelId;
        firstModel["displayName"] = "Duplicate";
        firstModel["providerModelId"] = "provider-duplicate";

        secondModel["id"] = DuplicateModelId;
        secondModel["displayName"] = "Duplicate 2";
        secondModel["providerModelId"] = "provider-duplicate-2";

        models.Add(secondModel);

        return catalog.ToJsonString();
    }

    private static (JsonObject Catalog, JsonObject Model) CreateValidModelJson()
    {
        if (JsonNode.Parse(CreateValidJson()) is not JsonObject parsedCatalog)
        {
            throw new InvalidOperationException("Valid model catalog test JSON is not an object.");
        }

        JsonArray models = GetModels(parsedCatalog);
        JsonObject model = models[0] as JsonObject
            ?? throw new InvalidOperationException("Valid model catalog test JSON has no model object.");

        return (parsedCatalog, model);
    }

    private static JsonArray GetModels(JsonObject catalog)
    {
        return catalog["models"] as JsonArray
            ?? throw new InvalidOperationException("Valid model catalog test JSON has no models array.");
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
