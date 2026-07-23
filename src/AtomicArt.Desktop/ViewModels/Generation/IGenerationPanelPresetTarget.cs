namespace AtomicArt.Desktop.ViewModels.Generation;

public interface IGenerationPanelPresetTarget
{
    event EventHandler? PresetAvailabilityChanged;

    bool CanApplyPreset(GenerationPanelPreset preset);

    void ApplyPreset(GenerationPanelPreset preset);
}
