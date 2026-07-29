using System.Globalization;

namespace AtomicArt.Desktop.Services.Localization;

public sealed record LocalizationOption(
    string Id,
    CultureInfo Culture,
    bool IsBuiltIn);
