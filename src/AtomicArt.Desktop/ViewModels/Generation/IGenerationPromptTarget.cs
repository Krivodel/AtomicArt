using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.ViewModels.Generation;

public interface IGenerationPromptTarget
{
    IRelayCommand<string?> ReplacePromptCommand { get; }
}
