namespace AtomicArt.Infrastructure.Tests.Generation.GoogleInteractions;

internal static class GoogleInteractionsResponseJsonTestFactory
{
    private const string DefaultUsageProperties = """
        "total_input_tokens": 1200,
        "total_output_tokens": 1120,
        "total_tokens": 2320
        """;

    public static string CreateCompletedImageResponse()
    {
        return CreateCompletedImageResponse(DefaultUsageProperties);
    }

    public static string CreateCompletedImageResponse(string usageProperties)
    {
        ArgumentNullException.ThrowIfNull(usageProperties);

        return CreateCompletedImageResponse(
            "image/jpeg",
            "/9j/4AAQSkZJRg==",
            usageProperties);
    }

    public static string CreateCompletedImageResponseWithoutUsage(
        string contentType,
        string base64Data)
    {
        return CreateCompletedImageResponse(contentType, base64Data, null);
    }

    public static string CreateCompletedImageResponseWithAdditionalUsage(
        string additionalUsageProperties)
    {
        ArgumentNullException.ThrowIfNull(additionalUsageProperties);

        return CreateCompletedImageResponse(
            $"{DefaultUsageProperties}{additionalUsageProperties}");
    }

    private static string CreateCompletedImageResponse(
        string contentType,
        string base64Data,
        string? usageProperties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Data);

        string usageSection = usageProperties is null
            ? string.Empty
            : $$"""
                ,
                "usage": {
                  {{usageProperties}}
                }
                """;

        return $$"""
            {
              "status": "completed",
              "steps": [
                {
                  "content": [
                  {
                    "type": "image",
                    "mime_type": "{{contentType}}",
                    "data": "{{base64Data}}"
                  }
                ]
              }
              ]{{usageSection}}
            }
            """;
    }
}
