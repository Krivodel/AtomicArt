using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace AtomicArt.Desktop.Services;

internal static class JsonFileSerializerOptions
{
    internal static JsonSerializerOptions Create(bool writeIndented = true)
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = writeIndented
        };
    }
}
