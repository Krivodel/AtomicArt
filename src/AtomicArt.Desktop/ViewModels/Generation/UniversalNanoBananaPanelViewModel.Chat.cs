using System.ComponentModel;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class UniversalNanoBananaPanelViewModel
{
    public GenerationChatViewModel? Chat { get; private set; }
    public bool IsChatPromptLocked => Chat is { IsExpanded: true, CanAddVariants: true }
        && Chat.IsForModel(SelectedModel?.Id);

    private string? _lockedChatPrompt;

    internal void ConnectChat(GenerationChatViewModel chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        DisconnectChat();
        Chat = chat;
        Chat.PropertyChanged += OnChatStateChanged;
        OnPropertyChanged(nameof(Chat));
        RefreshChatPrompt();
    }

    internal void DisconnectChat()
    {
        if (Chat is not null)
        {
            Chat.PropertyChanged -= OnChatStateChanged;
            Chat = null;
            RefreshChatPrompt();
        }
    }

    private void RefreshChatPrompt()
    {
        string? previous = _lockedChatPrompt;
        _lockedChatPrompt = IsChatPromptLocked ? Chat?.RepeatPrompt : null;
        OnPropertyChanged(nameof(IsChatPromptLocked));
        ReplacePromptCommand.NotifyCanExecuteChanged();

        if (_lockedChatPrompt is { } prompt)
        {
            Prompt = prompt;
        }
        else if (previous is not null && string.Equals(Prompt, previous, StringComparison.Ordinal))
        {
            Prompt = string.Empty;
        }
    }

    private void OnChatStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GenerationChatViewModel.CanSubmit) or nameof(GenerationChatViewModel.IsExpanded)
            or nameof(GenerationChatViewModel.CanAddVariants) or nameof(GenerationChatViewModel.RepeatPrompt))
        {
            RefreshChatPrompt();
            GenerateCommand.NotifyCanExecuteChanged();
        }
    }
}
