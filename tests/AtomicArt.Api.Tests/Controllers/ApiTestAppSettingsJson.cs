using System.Text.Json;

using AtomicArt.Infrastructure.Generation.GoogleInteractions;

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
          "GoogleInteractions": {
            "BaseUrl": {{JsonSerializer.Serialize(GoogleInteractionsOptions.DefaultBaseUrl)}},
            "TimeoutSeconds": 100
          },
          "TestGeneration": {
            "Enabled": {{testGenerationEnabled.ToString().ToLowerInvariant()}},
            "ImagesDirectory": {{JsonSerializer.Serialize(imagesDirectory)}}
          },
          "AllowedHosts": "*"
        }
        """;
    }
}
