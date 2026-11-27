using System.ComponentModel;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class UniversalNanoBananaPanelViewModel
{
    public GenerationChatViewModel? Chat { get; private set; }
    public bool RequiresChatBranchSelection => Chat is { IsExpanded: true, CanAddVariants: true }
        && Chat.IsForModel(SelectedModel?.Id);

    internal void ConnectChat(GenerationChatViewModel chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        DisconnectChat();
        Chat = chat;
        Chat.PropertyChanged += OnChatStateChanged;
        OnPropertyChanged(nameof(Chat));
        RefreshChatState();
    }

    internal void DisconnectChat()
    {
        if (Chat is not null)
        {
            Chat.PropertyChanged -= OnChatStateChanged;
            Chat = null;
            RefreshChatState();
        }
    }

    private void RefreshChatState()
    {
        OnPropertyChanged(nameof(RequiresChatBranchSelection));
        OnPropertyChanged(nameof(GenerateButtonText));
        GenerateCommand.NotifyCanExecuteChanged();
    }

    private void OnQuoteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NanoBanana2QuoteViewModel.GenerateButtonText))
        {
            OnPropertyChanged(nameof(GenerateButtonText));
        }
    }

    private void OnChatStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GenerationChatViewModel.CanSubmit) or nameof(GenerationChatViewModel.IsExpanded)
            or nameof(GenerationChatViewModel.CanAddVariants) or nameof(GenerationChatViewModel.RepeatPrompt))
        {
            RefreshChatState();
        }
    }
}
