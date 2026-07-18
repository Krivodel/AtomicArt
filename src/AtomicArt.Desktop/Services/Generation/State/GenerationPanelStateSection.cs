using System.Text.Json;

using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Generation.State;

public sealed class GenerationPanelStateSection : IStateSection
{
    private const string SectionKey = "generationPanels";
    private const string SectionFileName = "generation-panels.json";
    private const int CurrentSchemaVersion = 1;

    public string Key => SectionKey;
    public string FileName => SectionFileName;
    public int SchemaVersion => CurrentSchemaVersion;
    public Type PayloadType => typeof(GenerationPanelsState);

    public object CreateDefaultPayload()
    {
        return new GenerationPanelsState();
    }

    public object DeserializePayload(
        int schemaVersion,
        JsonElement payload,
        JsonSerializerOptions options)
    {
        GenerationPanelsState? state = payload.Deserialize<GenerationPanelsState>(options);

        if (state?.Panels is null)
        {
            return new GenerationPanelsState();
        }

        Dictionary<string, GenerationPanelState> panels =
            new Dictionary<string, GenerationPanelState>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, GenerationPanelState> pair in state.Panels)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
            {
                continue;
            }

            GenerationPanelState panelState = SanitizePanelState(pair.Key, pair.Value);
            panels[pair.Key] = panelState;
        }

        return new GenerationPanelsState
        {
            Panels = panels
        };
    }

    private static GenerationPanelState SanitizePanelState(
        string panelId,
        GenerationPanelState state)
    {
        return new GenerationPanelState
        {
            PanelId = string.IsNullOrWhiteSpace(state.PanelId) ? panelId : state.PanelId,
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
