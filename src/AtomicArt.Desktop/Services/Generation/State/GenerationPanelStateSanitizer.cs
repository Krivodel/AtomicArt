namespace AtomicArt.Desktop.Services.Generation.State;

internal static class GenerationPanelStateSanitizer
{
    internal static GenerationPanelState CreateSanitizedCopy(
        string panelId,
        GenerationPanelState state)
    {
        return new GenerationPanelState
        {
            PanelId = panelId,
            SelectedModelId = state.SelectedModelId ?? string.Empty,
            AspectRatio = state.AspectRatio ?? string.Empty,
            Resolution = state.Resolution ?? string.Empty,
            Temperature = state.Temperature,
            ThinkingLevel = state.ThinkingLevel,
            GenerationCount = state.GenerationCount,
            Prompt = state.Prompt ?? string.Empty,
            Attachments = PanelAttachmentStateSanitizer.Sanitize(state.Attachments)
        };
    }
}
