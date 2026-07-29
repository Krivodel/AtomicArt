using System.Globalization;
using System.Text.Json;

namespace AtomicArt.Desktop.Services.Localization;

internal sealed record LocalizationDocument(
    CultureInfo Culture,
    IReadOnlyDictionary<string, string> Strings,
    JsonElement StringsElement);
