using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

internal static class LocalCodexReasoningEffort
{
    public const string Default = "xhigh";

    public static string? GetLocalizationKey(string effort)
    {
        return effort switch
        {
            "none" => GenerationUiLocalizationKeys.Thinking.None,
            "minimal" => GenerationUiLocalizationKeys.Thinking.Minimal,
            "low" => GenerationLocalizationKeys.ThinkingLow,
            "medium" => GenerationUiLocalizationKeys.Thinking.Medium,
            "high" => GenerationLocalizationKeys.ThinkingHigh,
            Default => GenerationUiLocalizationKeys.Thinking.ExtraHigh,
            "max" => GenerationUiLocalizationKeys.Thinking.Maximum,
            "ultra" => GenerationUiLocalizationKeys.Thinking.Ultra,
            _ => null
        };
    }
}
