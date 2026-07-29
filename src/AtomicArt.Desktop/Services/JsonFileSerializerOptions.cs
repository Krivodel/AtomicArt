using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace AtomicArt.Desktop.Services;

internal static class JsonFileSerializerOptions
{
    internal static JsonSerializerOptions Create()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true
        };
    }
}
