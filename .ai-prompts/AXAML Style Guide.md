# AXAML Style Guide

## Avalonia 12 Baseline

Use Avalonia UI terminology and file conventions consistently.

- Use `.axaml` and `.axaml.cs` files, not WPF `.xaml` files.
- Use `App.axaml` for application resources and styles.
- Include a base theme in `Application.Styles`, normally `<FluentTheme />`.
- Treat compiled bindings as the default binding mode. Add `x:DataType` to views, data templates, and control templates that contain bindings.
- Use `{ReflectionBinding ...}` only for dynamic or late-bound paths that cannot be compiled.
- Bind Boolean view state directly to `IsVisible`; do not introduce WPF-style `Visibility` converters.
- Use Avalonia properties: `StyledProperty<T>`, `DirectProperty<T>`, and attached properties. Do not use `DependencyProperty`.
- Use Avalonia style selectors, classes, pseudo-classes, and `ControlTheme`. Do not use WPF `TargetType` styles, `Trigger`, `DataTrigger`, or `VisualStateManager` patterns.
- Use `IStorageProvider` behind an application service abstraction for file pickers. Do not use `OpenFileDialog` or `SaveFileDialog`.
- Use Avalonia 12 built-in page navigation for application navigation: `ContentPage` for screens, `NavigationPage` for stack navigation, `DrawerPage` for drawer/shell layouts, `TabbedPage` for tabbed areas, and `CommandBar` for page actions. Do not build a custom navigation stack, drawer shell, or page host when the built-in controls cover the scenario.

---

## File Organization

| Element | Pattern | Example |
|---------|---------|---------|
| View | `{Name}View.axaml` | `OrderListView.axaml` |
| UserControl | `{Name}Control.axaml` | `SearchBarControl.axaml` |
| Window | `{Name}Window.axaml` | `MainWindow.axaml` |
| Resource dictionary | `{Purpose}Resources.axaml` | `Colors.axaml`, `Brushes.axaml` |
| Style dictionary | `{Purpose}Styles.axaml` | `ButtonStyles.axaml`, `TextStyles.axaml` |
| Templated control theme | `Themes/Generic.axaml` | `Themes/Generic.axaml` |

### Project Structure

```text
Desktop/
├── App.axaml
├── App.axaml.cs
├── Views/
│   ├── Orders/
│   │   ├── OrderListView.axaml
│   │   ├── OrderListView.axaml.cs
│   │   └── OrderDetailView.axaml
│   └── Shell/
│       └── MainWindow.axaml
├── ViewModels/
├── Resources/
│   ├── Colors.axaml
│   ├── Brushes.axaml
│   ├── Spacing.axaml
│   ├── TextStyles.axaml
│   ├── ButtonStyles.axaml
│   ├── Templates.axaml
│   └── Converters.axaml
├── Converters/
├── Controls/
│   ├── SearchBox.cs
│   └── Themes/
│       └── Generic.axaml
└── Services/
```

---

## Root Markup

Every view with bindings must declare a data type for compiled bindings.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ProjectName.Desktop.ViewModels.Orders"
             x:Class="ProjectName.Desktop.Views.Orders.OrderListView"
             x:DataType="vm:OrderListViewModel">
    <!-- content -->
</UserControl>
```

Data templates must also declare `x:DataType`:

```xml
<DataTemplate x:DataType="dto:OrderDto">
    <TextBlock Text="{Binding Number}" />
</DataTemplate>
```

When a binding is intentionally dynamic, be explicit:

```xml
<TextBlock Text="{ReflectionBinding DynamicPropertyName}" />
```

---

## AXAML Attribute Order

Strict ordering for all AXAML elements:

1. `x:Class` / `x:Key` / `x:Name` / `x:DataType`
2. Attached layout properties (`Grid.Row`, `Grid.Column`, `DockPanel.Dock`)
3. Width / Height / Margin / Padding / Alignment
4. State and styling properties (`Classes`, `Theme`, `IsVisible`, `IsEnabled`)
5. Other properties in alphabetical order
6. `Binding` / `Command` properties last

### Multi-Line Formatting

When an element has **2 or more attributes**, place the **first attribute on the same line** as the opening tag. Subsequent attributes go on separate lines, aligned to the first attribute. Only elements with a **single attribute** may remain on one line.

```xml
<!-- ❌ WRONG — first attribute on its own line -->
<TextBox
    x:Name="NameTextBox"
    Grid.Row="1"
    Margin="8" />

<!-- ✅ CORRECT — first attribute on the same line, rest aligned -->
<TextBox x:Name="NameTextBox"
         Grid.Row="1"
         Margin="8" />
```

Do not place two attributes on a single line:

```xml
<!-- ❌ WRONG — 2 attributes on one line -->
<TextBlock Grid.Row="0" Text="{Binding Title}" />

<!-- ✅ CORRECT — 2 attributes: multi-line -->
<TextBlock Grid.Row="0"
           Text="{Binding Title}" />

<!-- ✅ CORRECT — 1 attribute: single line is fine -->
<TextBlock Text="{Binding Title}" />
```

---

## Nesting Depth Limit

Treat **5 levels** of layout-container nesting as a complexity warning. If markup goes deeper, check whether extracting a `UserControl`, `DataTemplate`, or dedicated custom control would make the view clearer.

Deeper nesting is allowed for control templates, data templates, and complex layouts when the structure is intentional and still readable.

---

## Hardcoded Values

Use resources and style classes for values that are semantic, repeated, theme-dependent, part of the design system, or shared between controls.

Do not create a resource just to name a value that is private to a single element and has no expected reuse. Inline values are allowed when all of these are true:

1. The value is used once.
2. The value is a local implementation detail of this view or template, not a brand color, spacing scale, typography rule, or public control contract.
3. The value does not need to change with theme, density, localization, or accessibility settings.
4. Reusing the same value elsewhere would be coincidental, not intentional.

When in doubt, keep semantic design values in resources and leave local mechanical values inline.

```xml
<!-- ❌ FORBIDDEN — semantic/reusable visual rule hidden as literals -->
<TextBlock Foreground="#FF0000"
           FontSize="14" />

<!-- ✅ CORRECT — semantic state uses a class/style -->
<TextBlock Classes="error" />
```

```xml
<Style Selector="TextBlock.error">
    <Setter Property="Foreground"
            Value="{StaticResource ErrorBrush}" />
    <Setter Property="FontSize"
            Value="{StaticResource BodyFontSize}" />
</Style>
```

```xml
<!-- ✅ CORRECT — one-off visual detail, not a reusable design rule -->
<Border Grid.Row="1"
        Height="1"
        Opacity="0.35" />
```

```xml
<!-- ❌ WRONG — resource created only for a private one-off detail -->
<x:Double x:Key="OrderHeaderSeparatorHeight">1</x:Double>
```

---

## Hardcoded Strings

Use localization resources for text that must be translated, reused, shared across features, or kept consistent with other UI copy.

Do not move every string literal into a constant or localization key by default. Inline strings are allowed when all of these are true:

1. The string is used once.
2. The string belongs to a screen-private or template-private element.
3. The feature is not inside a localized UI boundary.
4. The string is not a common action, status, error message, product term, accessibility label, or public control contract.

Within a localized feature, all production user-facing text must use the project-approved localization mechanism consistently. Do not mix `.resx`, JSON, and custom localization bindings in the same feature.

```xml
<!-- ❌ FORBIDDEN — common reusable UI copy -->
<TextBlock Text="Loading..." />

<!-- ✅ CORRECT -->
<TextBlock Text="{x:Static resources:UiStrings.Loading}" />
```

```xml
<!-- ✅ CORRECT — one-off private text in a non-localized view -->
<TextBlock Text="Preview is not available for this item" />
```

---

## Resource Dictionaries and Styles

### Merging Order in App.axaml

Application resources and styles are separate concerns. Keep value resources in `Application.Resources`; keep styles and themes in `Application.Styles`.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="ProjectName.Desktop.App">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <MergeResourceInclude Source="avares://ProjectName.Desktop/Resources/Colors.axaml" />
                <MergeResourceInclude Source="avares://ProjectName.Desktop/Resources/Brushes.axaml" />
                <MergeResourceInclude Source="avares://ProjectName.Desktop/Resources/Spacing.axaml" />
                <MergeResourceInclude Source="avares://ProjectName.Desktop/Resources/Converters.axaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>

    <Application.Styles>
        <FluentTheme />
        <StyleInclude Source="avares://ProjectName.Desktop/Resources/TextStyles.axaml" />
        <StyleInclude Source="avares://ProjectName.Desktop/Resources/ButtonStyles.axaml" />
        <StyleInclude Source="avares://ProjectName.Desktop/Resources/Templates.axaml" />
    </Application.Styles>
</Application>
```

Order: colors → brushes → spacing → converters → base theme → typography → control styles → templates.

### Color and Brush Separation

Brushes must reference `Color` resources. Do not define hex values directly in `SolidColorBrush` resources unless the value is a local, one-off drawing primitive inside a template.

```xml
<!-- Colors.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Color x:Key="PrimaryAccentColor">#0078D4</Color>
    <Color x:Key="ErrorColor">#D32F2F</Color>
    <Color x:Key="SuccessColor">#2E7D32</Color>
    <Color x:Key="TextPrimaryColor">#212121</Color>
    <Color x:Key="TextSecondaryColor">#757575</Color>
    <Color x:Key="BorderColor">#E0E0E0</Color>
</ResourceDictionary>
```

```xml
<!-- Brushes.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="PrimaryAccentBrush"
                     Color="{StaticResource PrimaryAccentColor}" />
    <SolidColorBrush x:Key="ErrorBrush"
                     Color="{StaticResource ErrorColor}" />
    <SolidColorBrush x:Key="TextPrimaryBrush"
                     Color="{StaticResource TextPrimaryColor}" />
</ResourceDictionary>
```

### Resource Key Naming

Pattern: `<Category><DescriptiveName>` in PascalCase.

```xml
<Color x:Key="PrimaryAccentColor">#0078D4</Color>
<SolidColorBrush x:Key="PrimaryAccentBrush"
                 Color="{StaticResource PrimaryAccentColor}" />
<x:Double x:Key="BodyFontSize">14</x:Double>
<Thickness x:Key="CardPadding">16</Thickness>
```

Do not duplicate reusable resources across dictionaries. Define common values once and reference them everywhere. Do not promote private one-off values into shared dictionaries only to avoid a literal.

---

## StaticResource vs DynamicResource

- Use `StaticResource` by default.
- Use `DynamicResource` only when the resource changes at runtime, for example theme switching or live language switching.
- Prefer theme dictionaries and `DynamicResource` for values that must follow the active theme variant.

---

## Bindings

### Long Binding Expressions

Break long binding expressions onto multiple lines:

```xml
<TextBox Text="{Binding Path=CustomerName,
                        Mode=TwoWay,
                        UpdateSourceTrigger=PropertyChanged}" />
```

### Element and Ancestor Bindings

Prefer Avalonia shorthand bindings for new AXAML:

- `#elementName.Property` for element bindings
- `$parent[Type].Property` for ancestor bindings

`ElementName` and `RelativeSource` are supported Avalonia binding syntax. Do not reject them as technical errors. They may be flagged as style issues only when the project standard requires shorthand syntax.

```xml
<TextBox x:Name="FilterTextBox" />
<TextBlock Text="{Binding #FilterTextBox.Text}" />
```

```xml
<TextBlock Text="{Binding $parent[Window].DataContext.Title}" />
```

Supported long-form alternatives:

```xml
<TextBox Name="FilterTextBox" />
<TextBlock Text="{Binding Text, ElementName=FilterTextBox}" />
```

```xml
<TextBlock Text="{Binding DataContext.Title,
                          RelativeSource={RelativeSource FindAncestor,
                          AncestorType=Window}}" />
```

Prefer normal ViewModel bindings over element and ancestor bindings. Use element/ancestor access only for view-local state.

### x:Name

Use `x:Name` only for view-local concerns that genuinely need a named element: code-behind view behavior, Avalonia element binding (`#ElementName.Property`), focus management, view-only animations, template parts, and stable UI test hooks. Prefer normal ViewModel bindings for application state.

Avoid `x:Name` on elements inside `DataTemplate` unless required by template logic, view-only behavior, or test hooks.

### No x:Code

`x:Code` in AXAML is forbidden.

---

## Styles, Classes, and Control Themes

### Inline Styles — Forbidden

Define styles in a style dictionary or `App.axaml`. Never put style blocks inline inside a feature view unless the style is private to a small `DataTemplate` and cannot be reused.

### Selectors Instead of WPF TargetType Styles

Application-wide defaults use Avalonia selectors:

```xml
<Style Selector="Button">
    <Setter Property="MinHeight"
            Value="{StaticResource ButtonMinHeight}" />
</Style>
```

Use classes for variants:

```xml
<Button Classes="primary"
        Command="{Binding SaveCommand}"
        Content="{x:Static resources:UiStrings.Save}" />
```

```xml
<Style Selector="Button.primary">
    <Setter Property="Background"
            Value="{StaticResource PrimaryAccentBrush}" />
</Style>

<Style Selector="Button.primary:pointerover">
    <Setter Property="Opacity"
            Value="0.92" />
</Style>
```

Do not use WPF triggers. Use Avalonia pseudo-classes for interaction states, bind view state to properties such as `IsVisible` and `IsEnabled`, or implement a custom pseudo-class in a custom control when necessary.

### ControlTheme for Reusable Control Appearance

Use `ControlTheme` when the full reusable appearance of a control is being defined.

```xml
<ControlTheme x:Key="PrimaryButtonTheme"
              TargetType="Button">
    <Setter Property="MinHeight"
            Value="{StaticResource ButtonMinHeight}" />
    <Setter Property="Background"
            Value="{StaticResource PrimaryAccentBrush}" />
</ControlTheme>
```

```xml
<Button Theme="{StaticResource PrimaryButtonTheme}"
        Command="{Binding SaveCommand}"
        Content="{x:Static resources:UiStrings.Save}" />
```

### Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Style class | Lowercase semantic name | `primary`, `danger`, `muted` |
| Style dictionary | `<Purpose>Styles.axaml` | `ButtonStyles.axaml` |
| ControlTheme | `<Descriptive><Control>Theme` | `PrimaryButtonTheme` |
| DataTemplate | `<DataType>Template` | `OrderItemTemplate` |

---

## Value Converters

### Rules

1. Stateless: no mutable fields, no side effects.
2. Business logic is forbidden; move it to the ViewModel or domain/application layer.
3. Handle `AvaloniaProperty.UnsetValue`, `BindingNotification`, and `null` gracefully.
4. Declare converters as singleton resources in a resource dictionary.
5. Prefer ViewModel Boolean properties over converters for common visibility and enabled-state logic.

### Naming Convention

`<SourceType>To<TargetType>Converter` or `<Purpose>Converter`:

```csharp
public sealed class DateTimeToRelativeStringConverter : IValueConverter { }
public sealed class InverseBooleanConverter : IValueConverter { }
public sealed class NullToBooleanConverter : IValueConverter { }
```

Do not create `BooleanToVisibilityConverter`; bind directly to `IsVisible`.

### Registration

```xml
<!-- Converters.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:converters="using:ProjectName.Desktop.Converters">
    <converters:InverseBooleanConverter x:Key="InverseBooleanConverter" />
    <converters:NullToBooleanConverter x:Key="NullToBooleanConverter" />
</ResourceDictionary>
```

Converters must be in separate files, never declared inline.

Prefer `IMultiValueConverter` over chained converters when conversion depends on two or more independent values.

---

## Avalonia Property Rules

### StyledProperty

Use `StyledProperty<T>` when a property must participate in Avalonia styling, binding, inheritance, or animation.

```csharp
public sealed class ItemsLimitBadge : TemplatedControl
{
    public static readonly StyledProperty<int> MaxItemsProperty =
        AvaloniaProperty.Register<ItemsLimitBadge, int>(
            nameof(MaxItems),
            defaultValue: 10);

    public int MaxItems
    {
        get => GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }
}
```

### DirectProperty

Use `DirectProperty<T>` for read-only or performance-sensitive properties that still need Avalonia binding support.

```csharp
public sealed class ItemsLimitBadge : TemplatedControl
{
    public static readonly DirectProperty<ItemsLimitBadge, int> CountProperty =
        AvaloniaProperty.RegisterDirect<ItemsLimitBadge, int>(
            nameof(Count),
            control => control.Count);

    private int _count;

    public int Count
    {
        get => _count;
        private set => SetAndRaise(CountProperty, ref _count, value);
    }
}
```

### Attached Properties

Attached properties use `RegisterAttached` and static accessors.

```csharp
public static class LayoutHints
{
    public static readonly AttachedProperty<bool> IsDenseProperty =
        AvaloniaProperty.RegisterAttached<LayoutHints, Control, bool>(
            "IsDense");

    public static bool GetIsDense(Control control)
    {
        return control.GetValue(IsDenseProperty);
    }

    public static void SetIsDense(Control control, bool value)
    {
        control.SetValue(IsDenseProperty, value);
    }
}
```

### Rules

1. Always use `nameof()` for property names when the Avalonia API accepts it.
2. CLR wrappers only call `GetValue`/`SetValue` or `SetAndRaise`; no business logic or service calls.
3. Use `StyledProperty<T>` for styleable/bindable/animatable control properties.
4. Use `DirectProperty<T>` for read-only or high-frequency properties.
5. Use attached properties only for view-level behavior and layout hints.
6. Do not use `DependencyProperty`.
7. For custom control validation in Avalonia 12, do not override validation infrastructure just to forward errors; use the built-in data validation support unless there is a control-specific reason.

---

## UserControl vs TemplatedControl

| Use UserControl when | Use TemplatedControl when |
|---------------------|---------------------------|
| Composite UI specific to the app | Reusable across projects or libraries |
| Fixed layout and fixed ViewModel/DataContext expectations | Visual structure should be replaceable via template |
| Application-specific screens and forms | The control exposes styleable Avalonia properties |

- Never put business logic or service dependencies in `UserControl` or `TemplatedControl`; delegate to ViewModel or application services.
- Templated controls go in `Controls/` with their default theme in `Themes/Generic.axaml`.
- Use `TemplateBinding` inside control templates when binding template parts to control properties.

---

## Common UI Patterns

### Loading State

```xml
<Grid>
    <ScrollViewer>
        <ItemsControl ItemsSource="{Binding Items}" />
    </ScrollViewer>

    <Border Background="{StaticResource LoadingOverlayBrush}"
            IsVisible="{Binding IsLoading}">
        <StackPanel HorizontalAlignment="Center"
                    VerticalAlignment="Center"
                    Spacing="{StaticResource SmallSpacing}">
            <ProgressBar Width="{StaticResource LoadingIndicatorWidth}"
                         IsIndeterminate="True" />
            <TextBlock HorizontalAlignment="Center"
                       Text="{x:Static resources:UiStrings.Loading}" />
        </StackPanel>
    </Border>
</Grid>
```

### Error Banner

```xml
<Border Padding="{StaticResource BannerPadding}"
        Background="{StaticResource ErrorBrush}"
        IsVisible="{Binding HasErrorMessage}">
    <TextBlock Foreground="{StaticResource OnErrorBrush}"
               Text="{Binding ErrorMessage}" />
</Border>
```

### File Picker Button

The view binds to a command. The ViewModel calls an application service abstraction. The Avalonia implementation of that service uses `IStorageProvider`.

```xml
<Button Command="{Binding PickFileCommand}"
        Content="{x:Static resources:UiStrings.PickFile}" />
```

---

## Avalonia 12 Migration Checklist

1. Rename WPF `.xaml` files to `.axaml`; rename `App.xaml` to `App.axaml`.
2. Replace WPF namespaces and `System.Windows.*` usages with Avalonia namespaces.
3. Add `x:DataType` to views, data templates, and control templates that contain bindings.
4. Replace dynamic binding paths with explicit `{ReflectionBinding ...}` only where compiled bindings cannot work.
5. Replace `DependencyProperty` with `StyledProperty<T>`, `DirectProperty<T>`, or attached properties.
6. Replace `Visibility` and visibility converters with `IsVisible` Boolean bindings.
7. Replace WPF `TargetType` styles and triggers with Avalonia selectors, classes, pseudo-classes, and `ControlTheme`.
8. When migrating WPF XAML to AXAML, prefer replacing `ElementName` and `RelativeSource AncestorType` patterns with `#element.Property` and `$parent[Type]` bindings for consistency. Do not treat remaining `ElementName` or `RelativeSource` usage as invalid by itself, because Avalonia supports both the long syntax and the shorthand syntax.
9. Replace `OpenFileDialog` and `SaveFileDialog` with `IStorageProvider` behind an application service.
10. Replace clipboard/drag-drop `IDataObject`/`DataObject` assumptions with Avalonia 12 data-transfer APIs when used.
11. Remove `Gestures.` attached-event prefixes; use gesture events directly on `InputElement`.
12. Use Avalonia dispatcher exception hooks, not WPF application dispatcher hooks.
13. Do not set `Window.WindowState` from a style; set it on the window instance or via view logic.
14. Remove `Avalonia.Diagnostics`; replace it with `AvaloniaUI.DiagnosticsSupport` and use `AttachDeveloperTools()` only for development builds.
15. If using TreeDataGrid, update TreeDataGrid package and column/source type names for the Avalonia 12-compatible version.
16. If using Avalonia headless tests, update test packages to the Avalonia 12-compatible test framework versions.

---

## Rules Summary

1. **Use `.axaml` and `App.axaml`** — no WPF `.xaml` conventions.
2. **Compiled bindings by default** — every binding scope declares `x:DataType` unless explicitly dynamic.
3. **No unnecessary resource extraction** — reusable, semantic, theme-dependent, localized, or shared values use resources/localization; private one-off values may stay inline.
4. **StaticResource by default** — `DynamicResource` only for runtime-changing resources.
5. **AXAML attribute order** — x directives → layout → size → state/style → other → binding/command.
6. **Maximum 5 layout-container levels** — extract complex markup.
7. **Styles use selectors/classes/pseudo-classes** — no WPF triggers.
8. **Use `ControlTheme` for reusable control appearance**.
9. **Converters are stateless and UI-only** — no business logic.
10. **Avalonia property system only** — no `DependencyProperty`.
11. **Bind Boolean state to `IsVisible`** — no visibility converters.
12. **Binding errors must be resolved, not ignored**.
13. **No `x:Code`, minimal `x:Name`**.
