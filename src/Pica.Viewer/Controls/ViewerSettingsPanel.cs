using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Pica.Viewer.Services;

namespace Pica.Viewer.Controls;

internal sealed class ViewerSettingsPanel : Border
{
    public ComboBox MovementSpeedComboBox { get; }
    public ComboBox ZoomSpeedComboBox { get; }
    public CheckBox ExpandOnDoubleClickCheckBox { get; }
    public CheckBox FastLoadingCheckBox { get; }
    public CheckBox AllowFreeZoomOutCheckBox { get; }
    public CheckBox SmoothPanningCheckBox { get; }
    public CheckBox PanningInertiaCheckBox { get; }
    public ComboBox ResizeBehaviorComboBox { get; }
    public CheckBox RememberWindowPlacementCheckBox { get; }

    private const double MaximumPanelWidth = 360d;
    private const double MinimumPanelWidth = 280d;
    private const double ControlSpacing = 8d;
    private const double SectionSpacing = 14d;

    public ViewerSettingsPanel(ImageViewerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        MovementSpeedComboBox = CreateSpeedComboBox(state.MovementSpeed);
        ZoomSpeedComboBox = CreateSpeedComboBox(state.ZoomSpeed);
        ExpandOnDoubleClickCheckBox = new CheckBox
        {
            Content = "Разворачивать двойным щелчком",
            IsChecked = state.ExpandOnDoubleClick
        };
        FastLoadingCheckBox = new CheckBox
        {
            Content = "Быстрая загрузка",
            IsChecked = state.IsFastLoadingEnabled
        };
        AllowFreeZoomOutCheckBox = new CheckBox
        {
            Content = "Свободное отдаление",
            IsChecked = state.AllowFreeZoomOut
        };
        SmoothPanningCheckBox = new CheckBox
        {
            Content = "Плавное перемещение",
            IsChecked = state.IsSmoothPanningEnabled
        };
        PanningInertiaCheckBox = new CheckBox
        {
            Content = "Инерция перемещения",
            IsChecked = state.IsPanningInertiaEnabled,
            IsEnabled = state.IsSmoothPanningEnabled
        };
        ResizeBehaviorComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewerSettingChoices.ResizeBehaviorOptions,
            SelectedItem = ViewerSettingChoices.ResizeBehaviorOptions.First(
                option => option.Value == state.ResizeBehavior)
        };
        RememberWindowPlacementCheckBox = new CheckBox
        {
            Content = "Запоминать положение и размер окна",
            IsChecked = state.RememberWindowPlacement
        };
        MinWidth = MinimumPanelWidth;
        MaxWidth = MaximumPanelWidth;
        Padding = new Thickness(16d);
        Classes.Add("modal-glass-panel");
        Child = CreateContent();
    }

    private StackPanel CreateContent()
    {
        StackPanel content = new()
        {
            Spacing = SectionSpacing
        };
        content.Children.Add(new TextBlock
        {
            FontSize = 17d,
            FontWeight = FontWeight.SemiBold,
            Text = "Настройки"
        });
        content.Children.Add(CreateLabeledControl("Скорость перемещения", MovementSpeedComboBox));
        content.Children.Add(SmoothPanningCheckBox);
        content.Children.Add(PanningInertiaCheckBox);
        content.Children.Add(CreateLabeledControl("Скорость масштабирования", ZoomSpeedComboBox));
        content.Children.Add(AllowFreeZoomOutCheckBox);
        content.Children.Add(CreateLabeledControl("Изменение размера окна", ResizeBehaviorComboBox));
        content.Children.Add(ExpandOnDoubleClickCheckBox);
        content.Children.Add(RememberWindowPlacementCheckBox);
        content.Children.Add(FastLoadingCheckBox);

        return content;
    }

    private static StackPanel CreateLabeledControl(string label, Control control)
    {
        StackPanel container = new()
        {
            Spacing = ControlSpacing
        };
        container.Children.Add(new TextBlock
        {
            Text = label
        });
        container.Children.Add(control);

        return container;
    }

    private static ComboBox CreateSpeedComboBox(int selectedSpeed)
    {
        return new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewerSettingChoices.SpeedOptions,
            SelectedItem = ViewerSettingChoices.SpeedOptions.First(
                option => option.Value == selectedSpeed)
        };
    }
}
