using Avalonia.Controls;

namespace AtomicArt.Desktop.Views.Gallery;

public interface IGenerationPreviewSessionFactory
{
    IGenerationPreviewSession Create(TopLevel topLevel);
}
