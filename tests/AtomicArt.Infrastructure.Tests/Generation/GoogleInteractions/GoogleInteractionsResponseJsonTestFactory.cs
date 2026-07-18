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
                {{usageProperties}}
              }
            }
            """;
    }

    public static string CreateCompletedImageResponseWithAdditionalUsage(
        string additionalUsageProperties)
    {
        ArgumentNullException.ThrowIfNull(additionalUsageProperties);

        return CreateCompletedImageResponse(
            $"{DefaultUsageProperties}{additionalUsageProperties}");
    }
}
