using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GeneratedGenerationItemStatusDescriptor : IRegisteredGenerationItemStatusDescriptor
{
    public GenerationItemStatus Status => GenerationItemStatus.Generated;
    public string DisplayText => _textProvider.Get(
        GenerationUiLocalizationKeys.Status.Generated);
    public GenerationItemVisualState VisualState => GenerationItemVisualState.Generated;
    public GenerationResultContentPolicy ResultContentPolicy => GenerationResultContentPolicy.SaveValidatedContent;

    private readonly ILocalizationTextProvider _textProvider;

    public GeneratedGenerationItemStatusDescriptor(
        ILocalizationTextProvider textProvider)
    {
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
    }
}
