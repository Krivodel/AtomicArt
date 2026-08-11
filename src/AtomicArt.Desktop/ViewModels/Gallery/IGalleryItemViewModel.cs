using System.ComponentModel;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public interface IGalleryItemViewModel : INotifyPropertyChanged
{
    Guid Id { get; }
    bool IsSelected { get; }
}
