using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class UniversalNanoBananaPanelViewModel
{
    private static readonly PresetParameterBinding[] PresetParameterBindings =
    [
        new PresetParameterBinding(
            GenerationParameterNames.Temperature,
            panel => panel.SupportsTemperature,
            (panel, model, name, settings) =>
                panel.Temperature = GenerationPanelOptionCompatibility.ResolveTemperature(
                    settings.GetNumber(name),
                    model.Temperature).Value),
        new PresetParameterBinding(
            GenerationParameterNames.Quality,
            panel => panel.SupportsQuality,
            (panel, _, name, settings) =>
                panel.SelectedQuality = panel.ResolveQuality(settings.GetString(name))),
        new PresetParameterBinding(
            GenerationParameterNames.Complexity,
            panel => panel.SupportsComplexity,
            (panel, _, name, settings) =>
                panel.SelectedComplexity = panel.ResolveComplexity(settings.GetString(name))),
        new PresetParameterBinding(
            GenerationParameterNames.ThinkingLevel,
            panel => panel.SupportsThinkingLevel,
            (panel, model, name, settings) =>
                panel.SelectedThinkingLevel = ResolveThinkingLevelOption(settings.GetString(name), model))
    ];

    private void ApplyPresetParameters(ImageModelOption model, GenerationRequestSettings settings)
    {
        foreach (PresetParameterBinding binding in PresetParameterBindings)
        {
            if (binding.IsSupported(this))
            {
                binding.Apply(this, model, binding.Name, settings);
            }
        }
    }

    private sealed record PresetParameterBinding(
        string Name,
        Func<UniversalNanoBananaPanelViewModel, bool> IsSupported,
        Action<UniversalNanoBananaPanelViewModel, ImageModelOption, string, GenerationRequestSettings> Apply);
}
