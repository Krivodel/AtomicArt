using Avalonia.Controls;

namespace AtomicArt.Desktop.Views;

public sealed record ViewTemplateRegistration(
    Type ViewModelType,
    Func<Control> CreateView);
