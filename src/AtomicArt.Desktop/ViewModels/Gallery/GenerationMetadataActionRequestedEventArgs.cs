using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed class GenerationMetadataActionRequestedEventArgs : EventArgs
{
    public IRelayCommand Command { get; }
    public object? Parameter { get; }

    public GenerationMetadataActionRequestedEventArgs(
        IRelayCommand command,
        object? parameter)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Parameter = parameter;
    }
}
