using AtomicArt.Infrastructure.Generation.GoogleInteractions;

namespace AtomicArt.Tests.Common.Generation;

public static class GoogleInteractionsTestConfiguration
{
    private const string TimeoutSecondsValue = "30";

    public static Dictionary<string, string?> Create()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [CreateKey(nameof(GoogleInteractionsOptions.TimeoutSeconds))] = TimeoutSecondsValue
        };
    }

    public static Dictionary<string, string?> CreateWithDefaultBaseUrl()
    {
        Dictionary<string, string?> values = Create();
        values[CreateKey(nameof(GoogleInteractionsOptions.BaseUrl))] =
            GoogleInteractionsOptions.DefaultBaseUrl;

        return values;
    }

    private static string CreateKey(string key)
    {
        return $"{GoogleInteractionsOptions.SectionName}:{key}";
    }
}
