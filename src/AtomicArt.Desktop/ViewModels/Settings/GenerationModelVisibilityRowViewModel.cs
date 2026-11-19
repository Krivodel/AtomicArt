namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed record GenerationModelVisibilityRowViewModel(
    string DisplayName,
    GenerationModelVisibilityOptionViewModel? OpenRouter,
    GenerationModelVisibilityOptionViewModel? Direct,
    bool StartsFamily = false)
{
    public IEnumerable<GenerationModelVisibilityOptionViewModel> Options
    {
        get
        {
            if (OpenRouter is not null)
            {
                yield return OpenRouter;
            }

            if (Direct is not null)
            {
                yield return Direct;
            }
        }
    }
}
