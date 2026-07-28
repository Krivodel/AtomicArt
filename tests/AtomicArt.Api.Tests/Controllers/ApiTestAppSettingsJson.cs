using System.Text.Json;

using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Api.Tests.Controllers;

internal static class ApiTestAppSettingsJson
{
    internal static string Create(bool testGenerationEnabled, string imagesDirectory)
    {
        return $$"""
        {
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "Generation": {
            "CopyBufferSize": 4096,
            "EmergencyMaxProviderResponseBytes": 1048576,
            "MaximumBoundaryLength": 256,
            "MaxConcurrentGenerations": 4,
            "MaxMetadataBytes": 1048576,
            "MaxRequestBytes": 1048576
          },
          "GoogleInteractions": {
            "BaseUrl": {{JsonSerializer.Serialize(GoogleInteractionsTestConfiguration.BaseUrl)}},
            "Base64InputBufferSize": 48,
            "Base64OutputBufferSize": 64,
            "InteractionsPath": "/v1beta/interactions",
            "MaxAnalyzedMetadataBytes": 4096,
            "MaxDiagnosticTextCharacters": 512,
            "MaxLoggedErrorMessageCharacters": 512,
            "MaxRequestBytes": 1048576,
            "MaxResponseBytes": 1048576,
            "MaxResponseStructureDepth": 64,
            "ProviderResponseTimeoutSeconds": 900,
            "ResponseBufferSize": 4096,
            "ServiceTier": "flex",
            "StoreInteractions": true
          },
          "TestGeneration": {
            "Base64InputBufferSize": 48,
            "Base64OutputBufferSize": 64,
            "Enabled": {{testGenerationEnabled.ToString().ToLowerInvariant()}},
            "FileStreamBufferSize": 4096,
            "GenerationDelayMilliseconds": 0,
            "ImagesDirectory": {{JsonSerializer.Serialize(imagesDirectory)}},
            "MaxImageBytes": 524288000
          },
          "AllowedHosts": "*"
        }
        """;
    }
}
