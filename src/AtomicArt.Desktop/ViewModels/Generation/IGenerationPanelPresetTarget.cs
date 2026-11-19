namespace AtomicArt.Desktop.ViewModels.Generation;

public interface IGenerationPanelPresetTarget
{
    event EventHandler? PresetAvailabilityChanged;

    bool CanApplyPreset(GenerationPanelPreset preset);

    Task<bool> TryApplyPresetAsync(GenerationPanelPreset preset, CancellationToken ct);
}
