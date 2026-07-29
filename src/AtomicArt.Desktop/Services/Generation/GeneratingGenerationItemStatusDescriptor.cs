using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GeneratingGenerationItemStatusDescriptor : IRegisteredGenerationItemStatusDescriptor
{
    public GenerationItemStatus Status => GenerationItemStatus.Generating;
    public string DisplayText => _textProvider.Get(
        GenerationUiLocalizationKeys.Status.Generating);
    public GenerationItemVisualState VisualState => GenerationItemVisualState.Generating;
    public GenerationResultContentPolicy ResultContentPolicy => GenerationResultContentPolicy.Ignore;

    private readonly ILocalizationTextProvider _textProvider;

    public GeneratingGenerationItemStatusDescriptor(
        ILocalizationTextProvider textProvider)
    {
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
    }
}
