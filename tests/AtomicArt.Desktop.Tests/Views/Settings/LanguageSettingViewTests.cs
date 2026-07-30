using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class LanguageSettingViewTests : DesktopControlTestBase
{
    [Fact]
    public async Task LanguageDropDown_WhenOpened_ShowsLocalizedSearchField()
    {
        await DispatchAsync(() =>
        {
            LanguageSettingViewModel viewModel = CreateViewModel();
            LanguageSettingView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                ComboBox comboBox = GetLanguageComboBox(view);

                comboBox.IsDropDownOpen = true;
                window.CaptureRenderedFrame();

                TextBox searchTextBox = GetSearchTextBox(comboBox);
                PathIcon searchIcon = GetSearchIcon(searchTextBox);
                IReadOnlyList<Button> clearButtons = searchTextBox
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .ToList();
                Rect searchIconBounds = GetTransformedBounds(
                    searchIcon,
                    searchTextBox);
                searchTextBox.IsEffectivelyVisible.Should().BeTrue();
                searchTextBox.PlaceholderText.Should().Be("Search");
                searchTextBox.IsFocused.Should().BeTrue();
                searchIcon.IsEffectivelyVisible.Should().BeTrue();
                searchIcon.IsHitTestVisible.Should().BeFalse();
                searchIcon.Margin.Left.Should().Be(6d);
                searchIconBounds.Left.Should().BeGreaterThan(
                    searchIcon.Margin.Left);
                clearButtons.Should().NotContain(button =>
                    button.IsEffectivelyVisible);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task LanguageDropDown_WithSearchText_HidesNonMatchingItemsAndPreservesSelection()
    {
        await DispatchAsync(() =>
        {
            LanguageSettingViewModel viewModel = CreateViewModel();
            LanguageSettingView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                ComboBox comboBox = GetLanguageComboBox(view);
                LanguageOptionViewModel? selectedOption =
                    viewModel.SelectedOption;

                comboBox.IsDropDownOpen = true;
                window.CaptureRenderedFrame();
                TextBox searchTextBox = GetSearchTextBox(comboBox);

                searchTextBox.Text = "Рус";
                window.CaptureRenderedFrame();

                IReadOnlyList<ComboBoxItem> items = GetDropDownItems(comboBox);
                ComboBoxItem englishItem = GetItem(items, "English");
                ComboBoxItem russianItem = GetItem(items, "Русский");
                englishItem.IsVisible.Should().BeFalse();
                russianItem.IsEffectivelyVisible.Should().BeTrue();
                viewModel.SelectedOption.Should().BeSameAs(selectedOption);
                comboBox.SelectedItem.Should().BeSameAs(selectedOption);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task LanguageDropDown_WhenClearButtonClicked_ClearsSearch()
    {
        await DispatchAsync(async () =>
        {
            LanguageSettingViewModel viewModel = CreateViewModel();
            LanguageSettingView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                ComboBox comboBox = GetLanguageComboBox(view);
                comboBox.IsDropDownOpen = true;
                window.CaptureRenderedFrame();
                TextBox searchTextBox = GetSearchTextBox(comboBox);
                searchTextBox.Text = "Рус";
                window.CaptureRenderedFrame();
                Button clearButton = GetClearButton(searchTextBox);
                Rect clearButtonBounds = GetTransformedBounds(
                    clearButton,
                    searchTextBox);

                clearButton.IsEffectivelyVisible.Should().BeTrue();
                ToolTip.GetTip(clearButton).Should().BeNull();
                clearButtonBounds.Center.X.Should().BeGreaterThan(
                    searchTextBox.Bounds.Width / 2d);
                searchTextBox.Focus();
                searchTextBox.IsFocused.Should().BeTrue();
                clearButton.Focus().Should().BeTrue();
                searchTextBox.IsFocused.Should().BeFalse();

                clearButton.RaiseEvent(new RoutedEventArgs(
                    Button.ClickEvent));
                await searchTextBox.Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Background);

                searchTextBox.Text.Should().BeEmpty();
                searchTextBox.IsFocused.Should().BeTrue();
                viewModel.SearchText.Should().BeEmpty();
                viewModel.Options.Should().OnlyContain(option =>
                    option.IsSearchMatch);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task LanguageDropDown_WhenClosed_ClearsSearch()
    {
        await DispatchAsync(async () =>
        {
            LanguageSettingViewModel viewModel = CreateViewModel();
            LanguageSettingView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                ComboBox comboBox = GetLanguageComboBox(view);
                comboBox.IsDropDownOpen = true;
                window.CaptureRenderedFrame();
                TextBox searchTextBox = GetSearchTextBox(comboBox);
                searchTextBox.Text = "Рус";
                searchTextBox.Focus();

                comboBox.IsDropDownOpen = false;
                await searchTextBox.Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Background);

                TextBoxFocusBehavior.GetFocusOnClear(searchTextBox)
                    .Should()
                    .BeFalse();
                searchTextBox.IsFocused.Should().BeFalse();
                viewModel.SearchText.Should().BeEmpty();
                viewModel.Options.Should().OnlyContain(option =>
                    option.IsSearchMatch);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task RegularDropDown_WhenOpened_KeepsSearchFieldHidden()
    {
        await DispatchAsync(() =>
        {
            ComboBox comboBox = new()
            {
                ItemsSource = new List<string>
                {
                    "First",
                    "Second"
                }
            };
            Window window = Show(comboBox);

            try
            {
                comboBox.IsDropDownOpen = true;
                window.CaptureRenderedFrame();

                TextBox searchTextBox = GetSearchTextBox(comboBox);
                searchTextBox.IsVisible.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    private static LanguageSettingViewModel CreateViewModel()
    {
        LocalizationOption english = new(
            "English",
            new System.Globalization.CultureInfo("en-US"),
            true);
        LocalizationOption russian = new(
            "Русский",
            new System.Globalization.CultureInfo("ru-RU"),
            true);
        TestLocalizationService localizationService = new()
        {
            AvailableLocalizations = new List<LocalizationOption>
            {
                english,
                russian
            },
            CurrentLocalization = english
        };

        return new LanguageSettingViewModel(
            new LanguageSettingDefinition(),
            localizationService,
            new NoOpSettingsStateService(),
            new TestViewModelErrorHandler(),
            localizationService);
    }

    private static ComboBox GetLanguageComboBox(
        LanguageSettingView view)
    {
        return view
            .GetVisualDescendants()
            .OfType<ComboBox>()
            .Single();
    }

    private static TextBox GetSearchTextBox(ComboBox comboBox)
    {
        return GetDropDownContent(comboBox)
            .GetVisualDescendants()
            .OfType<TextBox>()
            .Single();
    }

    private static PathIcon GetSearchIcon(TextBox searchTextBox)
    {
        return searchTextBox
            .GetVisualDescendants()
            .OfType<PathIcon>()
            .Single(icon => icon.Classes.Contains("search-icon"));
    }

    private static Button GetClearButton(TextBox searchTextBox)
    {
        return searchTextBox
            .GetVisualDescendants()
            .OfType<Button>()
            .Single();
    }

    private static Rect GetTransformedBounds(
        Control control,
        Visual target)
    {
        Matrix transform = control.TransformToVisual(target)
            ?? throw new InvalidOperationException(
                "Control transform was not found.");

        return new Rect(control.Bounds.Size).TransformToAABB(transform);
    }

    private static IReadOnlyList<ComboBoxItem> GetDropDownItems(
        ComboBox comboBox)
    {
        return GetDropDownContent(comboBox)
            .GetVisualDescendants()
            .OfType<ComboBoxItem>()
            .ToList();
    }

    private static Control GetDropDownContent(ComboBox comboBox)
    {
        Popup popup = comboBox
            .GetVisualDescendants()
            .OfType<Popup>()
            .Single(candidate => string.Equals(
                candidate.Name,
                "PART_Popup",
                StringComparison.Ordinal));
        Control popupContent = popup.Child
            ?? throw new InvalidOperationException(
                "ComboBox popup content was not found.");

        return popupContent;
    }

    private static ComboBoxItem GetItem(
        IReadOnlyList<ComboBoxItem> items,
        string displayName)
    {
        return items.Single(item =>
            item.Content is LanguageOptionViewModel option
            && string.Equals(
                option.DisplayName,
                displayName,
                StringComparison.Ordinal));
    }

    private sealed class NoOpSettingsStateService : ISettingsStateService
    {
        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public void ApplyValue(
            ISettingsDefinition definition,
            string value)
        {
        }

        public Task<string?> LoadValueAsync(
            ISettingsDefinition definition,
            CancellationToken ct)
        {
            return Task.FromResult<string?>(null);
        }

        public Task SaveValueAsync(
            ISettingsDefinition definition,
            string value,
            CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
