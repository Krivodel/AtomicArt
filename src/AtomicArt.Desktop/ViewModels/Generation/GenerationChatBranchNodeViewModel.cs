using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatBranchNodeViewModel : ObservableObject
{
    public Guid Id => _id;
    public Guid? ParentId => _parentId;
    public GenerationItemViewModel? Card { get; }
    public GenerationItemViewModel? PreviewCard => Card ?? _historyCard;
    public bool HasPreview => PreviewCard is not null;
    public bool IsDeleted => Card is null;
    public bool IsCurrent => _isCurrent;
    public bool HasStatus => IsDeleted || IsCurrent;
    public string StatusLocalizationKey => IsDeleted
        ? GenerationChatLocalizationKeys.DeletedChat : GenerationChatLocalizationKeys.CurrentChat;

    private GenerationItemViewModel? _historyCard;
    private Guid _id;
    private Guid? _parentId;
    private bool _isCurrent;
    [ObservableProperty]
    private bool _isSelected;

    internal GenerationChatBranchNodeViewModel(Guid id, Guid? parentId, GenerationItemViewModel? card, bool isCurrent)
    {
        _id = id;
        _parentId = parentId;
        Card = card;
        _isCurrent = isCurrent;
    }

    internal void Update(Guid id, Guid? parentId, bool isCurrent)
    {
        SetProperty(ref _id, id, nameof(Id));
        SetProperty(ref _parentId, parentId, nameof(ParentId));

        if (SetProperty(ref _isCurrent, isCurrent, nameof(IsCurrent)))
        {
            OnPropertyChanged(nameof(HasStatus));
        }
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
