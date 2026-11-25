using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatBranchNodeViewModel : ObservableObject
{
    public Guid Id { get; }
    public Guid? ParentId { get; }
    public GenerationItemViewModel? Card { get; }
    public GenerationItemViewModel? PreviewCard => Card ?? _historyCard;
    public bool HasPreview => PreviewCard is not null;
    public bool IsDeleted => Card is null;
    public bool IsCurrent { get; }
    public bool HasStatus => IsDeleted || IsCurrent;
    public string StatusLocalizationKey => IsDeleted
        ? GenerationChatLocalizationKeys.DeletedChat : GenerationChatLocalizationKeys.CurrentChat;

    private GenerationItemViewModel? _historyCard;
    [ObservableProperty]
    private bool _isSelected;

    internal GenerationChatBranchNodeViewModel(Guid id, Guid? parentId, GenerationItemViewModel? card, bool isCurrent)
    {
        Id = id;
        ParentId = parentId;
        Card = card;
        IsCurrent = isCurrent;
    }

    internal void RestorePreview(GenerationItemViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (IsDeleted && SetProperty(ref _historyCard, card, nameof(PreviewCard)))
        {
            OnPropertyChanged(nameof(HasPreview));
        }
    }
}
