using System.Text.Json;

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
            "BaseUrl": "https://generativelanguage.googleapis.com",
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
