using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AtomicArt.Desktop.Controls.Generation;

public sealed partial class GenerationOptionSelector : UserControl
{
    public static readonly StyledProperty<ComboBox?> SelectorProperty =
        AvaloniaProperty.Register<GenerationOptionSelector, ComboBox?>(nameof(Selector));
    public static readonly StyledProperty<ICommand?> ResetCommandProperty =
        AvaloniaProperty.Register<GenerationOptionSelector, ICommand?>(nameof(ResetCommand));
    public static readonly StyledProperty<Geometry?> ResetIconProperty =
        AvaloniaProperty.Register<GenerationOptionSelector, Geometry?>(nameof(ResetIcon));
    public static readonly StyledProperty<string?> ResetButtonNameProperty =
        AvaloniaProperty.Register<GenerationOptionSelector, string?>(nameof(ResetButtonName));

    public ComboBox? Selector
    {
        get => GetValue(SelectorProperty);
        set => SetValue(SelectorProperty, value);
    }
    public ICommand? ResetCommand
    {
        get => GetValue(ResetCommandProperty);
        set => SetValue(ResetCommandProperty, value);
    }
    public Geometry? ResetIcon
    {
        get => GetValue(ResetIconProperty);
        set => SetValue(ResetIconProperty, value);
    }
    public string? ResetButtonName
    {
        get => GetValue(ResetButtonNameProperty);
        set => SetValue(ResetButtonNameProperty, value);
    }

    public GenerationOptionSelector()
    {
        InitializeComponent();
    }
}
