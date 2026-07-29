using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class FailedGenerationItemStatusDescriptor : IRegisteredGenerationItemStatusDescriptor
{
    public GenerationItemStatus Status => GenerationItemStatus.Failed;
    public string DisplayText => _textProvider.Get(CommonLocalizationKeys.Error);
    public GenerationItemVisualState VisualState => GenerationItemVisualState.Failed;
    public GenerationResultContentPolicy ResultContentPolicy => GenerationResultContentPolicy.Ignore;

    private readonly ILocalizationTextProvider _textProvider;

    public FailedGenerationItemStatusDescriptor(
        ILocalizationTextProvider textProvider)
    {
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
    }
}
