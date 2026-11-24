using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed class GenerationChatGroupViewModel
{
    public GenerationChatGroup Group { get; }
    public string Prompt { get; }
    public string CountText { get; }
    public string Label => $"{Prompt} {CountText}";

    internal GenerationChatGroupViewModel(GenerationChatGroup group, string prompt, int count,
        NanoBanana2PanelTextFormatter formatter)
    {
        Group = group;
        Prompt = prompt;
        CountText = $"· {formatter.FormatChatVariantsCount(count)}";
    }
}
