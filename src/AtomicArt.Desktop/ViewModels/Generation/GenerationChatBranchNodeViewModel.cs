using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatBranchNodeViewModel : ObservableObject
{
    public Guid Id { get; }
    public Guid? ParentId { get; }
    public GenerationItemViewModel? Card { get; }
    public bool IsDeleted => Card is null;
    public bool IsCurrent { get; }

    [ObservableProperty]
    private bool _isSelected;

    internal GenerationChatBranchNodeViewModel(Guid id, Guid? parentId, GenerationItemViewModel? card, bool isCurrent)
    {
        Id = id;
        ParentId = parentId;
        Card = card;
        IsCurrent = isCurrent;
    }
}
