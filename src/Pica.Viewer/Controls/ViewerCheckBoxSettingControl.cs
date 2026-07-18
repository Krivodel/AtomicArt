using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Pica.Viewer.Controls;

internal sealed class ViewerCheckBoxSettingControl : ViewerSettingControl
{
    internal override Control Control => CheckBox;
    internal CheckBox CheckBox { get; }
    internal bool IsEnabled
    {
        get => CheckBox.IsEnabled;
        set => CheckBox.IsEnabled = value;
    }

    private readonly Func<bool, Task> _changed;
    private bool _isChangingValue;

    internal ViewerCheckBoxSettingControl(
        string content,
        bool initialValue,
        Func<bool, Task> changed,
        bool isEnabled = true)
        : base(null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));

        CheckBox = new CheckBox
        {
            Content = content,
            IsChecked = initialValue,
            IsEnabled = isEnabled
        };
        CheckBox.IsCheckedChanged += OnIsCheckedChanged;
    }

    internal void SetValue(bool value)
    {
        _isChangingValue = true;

        try
        {
            CheckBox.IsChecked = value;
        }
        finally
        {
            _isChangingValue = false;
        }
    }

    private async void OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isChangingValue)
        {
            return;
        }

        await _changed(CheckBox.IsChecked == true);
    }
}
