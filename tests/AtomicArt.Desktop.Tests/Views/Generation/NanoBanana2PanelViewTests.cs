using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Generation;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.Tests.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Generation;
using TestGenerationCredentials = AtomicArt.Tests.Common.Generation.TestGenerationCredentials;

namespace AtomicArt.Desktop.Tests.Views.Generation;

public sealed class NanoBanana2PanelViewTests : AnimatedGalleryControlTestBase
{
    private const double ExpandedPanelWidth = 640d;
    private const double ExpandedPanelHeight = 560d;
    private const string PromptHeightResourceKey = "PromptHeight";

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Авто", false)]
    [InlineData("1:1", true)]
    [InlineData("16:9", true)]
    public void CanShowAspectRatioHint_WithAspectRatio_ReturnsOnlyConcreteAspectRatios(
        string? aspectRatio,
        bool expectedResult)
    {
        bool result = NanoBanana2PanelView.CanShowAspectRatioHint(aspectRatio);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task SelectionValueReset_WhenResolutionResets_FlashesTemplateBackgroundThenFadesAndRestoresOriginalValue()
    {
        await DispatchAsync(async () =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            string unsupportedResolution = GetSelectedModel(viewModel).Resolutions.Last();
            ImageModelOption modelWithoutResolution = viewModel.AvailableModels.Single(model =>
                !model.Resolutions.Contains(unsupportedResolution, StringComparer.Ordinal));
            Window window = Show(view);

            try
            {
                ComboBox resolutionComboBox = GetComboBox(view, "ResolutionComboBox");
                Border? flashTarget = NanoBanana2PanelView.FindOptionResetFlashTarget(resolutionComboBox);
                flashTarget.Should().NotBeNull();
                if (flashTarget is null)
                {
                    throw new InvalidOperationException("ComboBox template flash target was not found.");
                }

                IBrush? originalBackground = flashTarget.Background;
                IBrush? originalBorderBrush = flashTarget.BorderBrush;
                originalBackground.Should().NotBeNull();

                viewModel.SelectedResolution = unsupportedResolution;
                viewModel.SelectedModel = modelWithoutResolution;

                Border? activeFlashTarget = NanoBanana2PanelView.FindOptionResetFlashTarget(resolutionComboBox);
                activeFlashTarget.Should().BeSameAs(flashTarget);
                view.GetVisualDescendants()
                    .OfType<Border>()
                    .Should()
                    .NotContain(border => string.Equals(
                        border.Name,
                        "ResolutionResetFlash",
                        StringComparison.Ordinal));
                resolutionComboBox
                    .GetVisualDescendants()
                    .Should()
                    .Contain(flashTarget);

                if (activeFlashTarget is null)
                {
                    throw new InvalidOperationException("ComboBox template flash target was not found after reset.");
                }

                activeFlashTarget.BorderBrush.Should().BeSameAs(originalBorderBrush);

                SolidColorBrush? flashBrush = activeFlashTarget.Background as SolidColorBrush;
                flashBrush.Should().NotBeNull();
                if (flashBrush is null)
                {
                    throw new InvalidOperationException("ComboBox template flash target did not receive a flash background.");
                }

                flashBrush.Should().NotBeSameAs(originalBackground);

                await Task.Delay(TimeSpan.FromMilliseconds(1000d));

                flashTarget.Background.Should().BeSameAs(flashBrush);

                await Task.Delay(TimeSpan.FromMilliseconds(300d));

                flashTarget.Background.Should().BeSameAs(originalBackground);
                flashTarget.BorderBrush.Should().BeSameAs(originalBorderBrush);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PromptTextBox_WhenPanelIsTallerThanMinimum_FillsAvailableHeight()
    {
        Dispatch(() =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view, ExpandedPanelWidth, ExpandedPanelHeight);

            try
            {
                TextBox promptTextBox = view
                    .GetVisualDescendants()
                    .OfType<TextBox>()
                    .Single();
                double promptMinimumHeight = GetPromptMinimumHeight(view);

                promptTextBox.MinHeight.Should().Be(promptMinimumHeight);
                promptTextBox.Bounds.Height.Should().BeGreaterThan(promptMinimumHeight);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Panel_WhenShownBeforeStateRestore_DoesNotLoadModelCatalog()
    {
        Dispatch(() =>
        {
            FixedGenerationModelCatalogApiClient catalogApiClient = new(
                ApiModelMetadataTestCatalog.LoadCatalog());
            ImageModelOptionCatalog imageModelOptionCatalog = new();
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
                catalogApiClient,
                imageModelOptionCatalog);
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                catalogApiClient.CallCount.Should().Be(0);
                imageModelOptionCatalog.IsLoaded.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PromptTextBox_WhenShown_HasHalfSpeedWheelScrolling()
    {
        Dispatch(() =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                ScrollViewer promptScrollViewer = view
                    .GetVisualDescendants()
                    .OfType<TextBox>()
                    .Single()
                    .GetVisualDescendants()
                    .OfType<ScrollViewer>()
                    .Single();

                SmoothScrollBehavior
                    .GetWheelMultiplier(promptScrollViewer)
                    .Should()
                    .Be(48d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TemperatureButton_WhenPanelIsShown_ContainsCatalogConfiguredSliderAndResetButton()
    {
        Dispatch(() =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                Button temperatureButton = GetTemperatureButton(view);
                Popup popup = GetTemperaturePopup(view);

                popup.IsOpen = true;

                Border content = GetTemperaturePopupContent(popup);
                Slider slider = content
                    .GetLogicalDescendants()
                    .OfType<Slider>()
                    .Single();
                Button resetButton = content
                    .GetLogicalDescendants()
                    .OfType<Button>()
                    .Single(button => button.Command == viewModel.ResetTemperatureCommand);

                slider.Minimum.Should().Be(viewModel.MinimumTemperature);
                slider.Maximum.Should().Be(viewModel.MaximumTemperature);
                slider.TickFrequency.Should().Be(viewModel.TemperatureStep);
                slider.Value.Should().Be(viewModel.DefaultTemperature);
                resetButton.Command.Should().BeSameAs(viewModel.ResetTemperatureCommand);

                popup.IsOpen = false;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SettingsFlyout_WhenPanelIsShown_UsesIconsAndCatalogThinkingLevels()
    {
        Dispatch(() =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                Button settingsButton = GetTemperatureButton(view);
                PathIcon settingsIcon = settingsButton.Content.Should()
                    .BeOfType<PathIcon>()
                    .Subject;
                settingsButton.TryFindResource("AppSettingsIcon", out object? settingsGeometry).Should().BeTrue();
                settingsIcon.Data.Should().BeSameAs(settingsGeometry);
                Popup popup = GetTemperaturePopup(view);

                popup.IsOpen = true;

                Border content = GetTemperaturePopupContent(popup);
                ThemeVariantScope themeScope = popup.Child.Should()
                    .BeOfType<ThemeVariantScope>()
                    .Subject;
                IBrush textBrush = GetBrushResource(content, "SukiText");
                Slider slider = content.GetLogicalDescendants().OfType<Slider>().Single();
                TextBlock temperatureValue = content.GetLogicalDescendants()
                    .OfType<TextBlock>()
                    .Single(textBlock => string.Equals(
                        textBlock.Text,
                        viewModel.TemperatureText,
                        StringComparison.Ordinal));
                PathIcon temperatureIcon = content.GetLogicalDescendants()
                    .OfType<PathIcon>()
                    .First();
                ComboBox thinkingComboBox = content.GetLogicalDescendants()
                    .OfType<ComboBox>()
                    .Single(comboBox => string.Equals(
                        comboBox.Name,
                        "ThinkingLevelComboBox",
                        StringComparison.Ordinal));
                IReadOnlyList<GenerationModelThinkingLevelMetadataDto> thinkingLevels = thinkingComboBox.ItemsSource
                    .Should()
                    .BeAssignableTo<IReadOnlyList<GenerationModelThinkingLevelMetadataDto>>()
                    .Subject;
                Button thinkingResetButton = content.GetLogicalDescendants()
                    .OfType<Button>()
                    .Single(button => button.Command == viewModel.ResetThinkingLevelCommand);
                Grid thinkingControls = thinkingComboBox.Parent.Should()
                    .BeOfType<Grid>()
                    .Subject;

                temperatureValue.Bounds.Top.Should().BeLessThan(slider.Bounds.Top);
                themeScope.ActualThemeVariant.Should().Be(ThemeVariant.Dark);
                temperatureValue.Foreground.Should().BeSameAs(textBrush);
                temperatureIcon.Foreground.Should().BeSameAs(textBrush);
                content.GetLogicalDescendants().OfType<TextBlock>()
                    .Select(textBlock => textBlock.Text)
                    .OfType<string>()
                    .Should()
                    .NotContain(text => text.Contains("Температура", StringComparison.Ordinal));
                thinkingLevels.Select(level => level.DisplayName).Should().Equal("Минимальный", "Максимальный");
                slider.Cursor.Should().Be(new Cursor(StandardCursorType.Hand));
                thinkingControls.IsVisible.Should().BeTrue();
                thinkingResetButton.Command.Should().BeSameAs(viewModel.ResetThinkingLevelCommand);

                ImageModelOption proModel = viewModel.AvailableModels.Single(model =>
                    model.Id == ApiModelMetadataTestCatalog.NanoBananaProModelId);
                viewModel.SelectedModel = proModel;

                thinkingControls.IsVisible.Should().BeFalse();
                thinkingComboBox.SelectedItem.Should().BeNull();

                popup.IsOpen = false;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TemperatureButton_WhenPanelIsShown_MatchesOptionHeightAndUsesLargerIcon()
    {
        Dispatch(() =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                Button temperatureButton = GetTemperatureButton(view);
                ComboBox resolutionComboBox = GetComboBox(view, "ResolutionComboBox");
                PathIcon temperatureIcon = temperatureButton
                    .GetVisualDescendants()
                    .OfType<PathIcon>()
                    .Single();

                temperatureButton.Bounds.Height.Should().Be(resolutionComboBox.Bounds.Height);
                temperatureButton.Bounds.Width.Should().Be(temperatureButton.Bounds.Height);
                temperatureIcon.Bounds.Width.Should().BeGreaterThan(16d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TemperatureControls_WhenPanelIsShown_HaveNoToolTips()
    {
        Dispatch(() =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                Button temperatureButton = GetTemperatureButton(view);
                Popup popup = GetTemperaturePopup(view);
                Border content = GetTemperaturePopupContent(popup);
                IReadOnlyList<Button> resetButtons = content
                    .GetLogicalDescendants()
                    .OfType<Button>()
                    .ToList();

                ToolTip.GetTip(temperatureButton).Should().BeNull();
                resetButtons.Should().AllSatisfy(button =>
                    ToolTip.GetTip(button).Should().BeNull());
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PopupThemeResources_WhenLoaded_AreOpaqueBlue()
    {
        Dispatch(() =>
        {
            NanoBanana2PanelView view = new();
            Window window = Show(view);

            try
            {
                Color popupBackground = GetColorResource(view, "SukiPopupBackground");
                Color selectedItemBackground = GetColorResource(view, "SukiLightBorderBrush");
                Color menuBorder = GetColorResource(view, "SukiMenuBorderBrush");

                popupBackground.A.Should().Be(byte.MaxValue);
                selectedItemBackground.A.Should().Be(byte.MaxValue);
                menuBorder.A.Should().Be(byte.MaxValue);
                popupBackground.B.Should().BeGreaterThan(popupBackground.R);
                selectedItemBackground.B.Should().BeGreaterThan(selectedItemBackground.R);
                menuBorder.B.Should().BeGreaterThan(menuBorder.R);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(0.6d)]
    [InlineData(1.5d)]
    public void TemperaturePopup_WhenPanelIsScaled_InheritsTransformWithoutClipping(
        double uiScale)
    {
        Dispatch(() =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            LayoutTransformControl scaleHost = new()
            {
                Child = view,
                LayoutTransform = new ScaleTransform(uiScale, uiScale)
            };
            Window window = Show(scaleHost);

            try
            {
                Button temperatureButton = GetTemperatureButton(view);
                Popup popup = GetTemperaturePopup(view);

                ToggleTemperaturePopup(temperatureButton);

                Border panel = GetTemperaturePopupContent(popup);
                Button temperatureResetButton = panel
                    .GetLogicalDescendants()
                    .OfType<Button>()
                    .Single(button => button.Command == viewModel.ResetTemperatureCommand);
                ComboBox thinkingComboBox = panel
                    .GetLogicalDescendants()
                    .OfType<ComboBox>()
                    .Single(comboBox => string.Equals(
                        comboBox.Name,
                        "ThinkingLevelComboBox",
                        StringComparison.Ordinal));
                Grid thinkingControls = thinkingComboBox.Parent.Should()
                    .BeOfType<Grid>()
                    .Subject;
                PopupRoot popupRoot = TopLevel.GetTopLevel(panel).Should()
                    .BeOfType<PopupRoot>()
                    .Subject;
                ScaleTransform inheritedScale = popupRoot.Transform.Should()
                    .BeOfType<ScaleTransform>()
                    .Subject;

                popup.InheritsTransform.Should().BeTrue();
                inheritedScale.ScaleX.Should().BeApproximately(uiScale, 0.001d);
                inheritedScale.ScaleY.Should().BeApproximately(uiScale, 0.001d);
                temperatureResetButton.IsEffectivelyVisible.Should().BeTrue();
                thinkingControls.IsEffectivelyVisible.Should().BeTrue();
                AssertContainedByPanel(temperatureResetButton, panel);
                AssertContainedByPanel(thinkingControls, panel);

                popup.IsOpen = false;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TemperatureFlyout_WhenOpenedAndClosed_UsesDistinctPanelAndFadeAnimation()
    {
        await DispatchAsync(async () =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                Button temperatureButton = GetTemperatureButton(view);
                Popup popup = GetTemperaturePopup(view);
                Border panel = GetTemperaturePopupContent(popup);
                TransformGroup motionTransform = panel.RenderTransform.Should()
                    .BeOfType<TransformGroup>()
                    .Subject;
                ScaleTransform motionScale = motionTransform.Children
                    .OfType<ScaleTransform>()
                    .Single();
                TranslateTransform motionTranslate = motionTransform.Children
                    .OfType<TranslateTransform>()
                    .Single();

                ToggleTemperaturePopup(temperatureButton);
                await Task.Delay(TimeSpan.FromMilliseconds(75d));

                Transitions transitions = panel.Transitions
                    ?? throw new InvalidOperationException("Temperature flyout transitions were not found.");
                DoubleTransition opacityTransition = transitions
                    .OfType<DoubleTransition>()
                    .Single(transition => transition.Property == Visual.OpacityProperty);
                panel.BorderThickness.Left.Should().BeGreaterThan(0d);
                panel.CornerRadius.TopLeft.Should().BeGreaterThan(0d);
                opacityTransition.Duration.Should().Be(TimeSpan.FromMilliseconds(150d));
                panel.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
                motionScale.ScaleX.Should().BeGreaterThan(0.94d).And.BeLessThan(1d);
                motionScale.ScaleY.Should().BeGreaterThan(0.94d).And.BeLessThan(1d);
                motionTranslate.Y.Should().BeGreaterThan(-10d).And.BeLessThan(0d);

                await Task.Delay(TimeSpan.FromMilliseconds(100d));

                panel.Opacity.Should().Be(1d);
                motionScale.ScaleX.Should().Be(1d);
                motionScale.ScaleY.Should().Be(1d);
                motionTranslate.Y.Should().Be(0d);

                ToggleTemperaturePopup(temperatureButton);

                popup.IsOpen.Should().BeTrue();

                await Task.Delay(TimeSpan.FromMilliseconds(75d));

                panel.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
                motionScale.ScaleX.Should().BeGreaterThan(0.94d).And.BeLessThan(1d);
                motionScale.ScaleY.Should().BeGreaterThan(0.94d).And.BeLessThan(1d);
                motionTranslate.Y.Should().BeGreaterThan(-10d).And.BeLessThan(0d);

                await Task.Delay(TimeSpan.FromMilliseconds(105d));

                popup.IsOpen.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ThinkingLevelDropDown_WhenItemIsClicked_KeepsSettingsPopupOpen()
    {
        await DispatchAsync(async () =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                Popup settingsPopup = GetTemperaturePopup(view);
                Button settingsButton = GetTemperatureButton(view);

                ToggleTemperaturePopup(settingsButton);

                Border settingsPanel = GetTemperaturePopupContent(settingsPopup);
                ComboBox thinkingComboBox = settingsPanel
                    .GetLogicalDescendants()
                    .OfType<ComboBox>()
                    .Single(comboBox => string.Equals(
                        comboBox.Name,
                        "ThinkingLevelComboBox",
                        StringComparison.Ordinal));
                thinkingComboBox.IsDropDownOpen = true;

                Popup dropDownPopup = thinkingComboBox
                    .GetVisualDescendants()
                    .OfType<Popup>()
                    .Single(popup => string.Equals(
                        popup.Name,
                        "PART_Popup",
                        StringComparison.Ordinal));
                Control dropDownContent = dropDownPopup.Child
                    ?? throw new InvalidOperationException("Thinking level popup content was not found.");
                TopLevel dropDownRoot = TopLevel.GetTopLevel(dropDownContent)
                    ?? throw new InvalidOperationException("Thinking level popup root was not found.");
                ComboBoxItem item = dropDownContent
                    .GetVisualDescendants()
                    .OfType<ComboBoxItem>()
                    .Last();
                Point itemCenter = item.TranslatePoint(
                        new Point(item.Bounds.Width / 2d, item.Bounds.Height / 2d),
                        dropDownRoot)
                    ?? throw new InvalidOperationException("Thinking level item position was not found.");

                dropDownRoot.MouseDown(itemCenter, MouseButton.Left);
                dropDownRoot.MouseUp(itemCenter, MouseButton.Left);
                await Task.Delay(TimeSpan.FromMilliseconds(200d));

                settingsPopup.IsOpen.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task SettingsPopup_WhenOutsideIsClicked_ClosesAfterAnimation()
    {
        await DispatchAsync(async () =>
        {
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            NanoBanana2PanelView view = new()
            {
                DataContext = viewModel
            };
            Window window = Show(view);

            try
            {
                Popup settingsPopup = GetTemperaturePopup(view);
                Button settingsButton = GetTemperatureButton(view);

                ToggleTemperaturePopup(settingsButton);

                Point outsidePoint = new(window.Bounds.Width - 1d, window.Bounds.Height - 1d);
                window.MouseDown(outsidePoint, MouseButton.Left);
                window.MouseUp(outsidePoint, MouseButton.Left);
                await Task.Delay(TimeSpan.FromMilliseconds(200d));

                settingsPopup.IsOpen.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ComboBox GetComboBox(NanoBanana2PanelView view, string name)
    {
        return view
            .GetVisualDescendants()
            .OfType<ComboBox>()
            .Single(comboBox => string.Equals(comboBox.Name, name, StringComparison.Ordinal));
    }

    private static Button GetTemperatureButton(NanoBanana2PanelView view)
    {
        return view
            .GetVisualDescendants()
            .OfType<Button>()
            .Single(button => string.Equals(button.Name, "TemperatureButton", StringComparison.Ordinal));
    }

    private static Popup GetTemperaturePopup(NanoBanana2PanelView view)
    {
        return view
            .GetVisualDescendants()
            .OfType<Popup>()
            .Single(popup => string.Equals(
                popup.Name,
                "TemperaturePopup",
                StringComparison.Ordinal));
    }

    private static Border GetTemperaturePopupContent(Popup popup)
    {
        ThemeVariantScope themeScope = popup.Child as ThemeVariantScope
            ?? throw new InvalidOperationException("Temperature popup theme scope was not found.");
        FlyoutPresenter presenter = themeScope.Child as FlyoutPresenter
            ?? throw new InvalidOperationException("Temperature popup presenter was not found.");

        return presenter.Content as Border
            ?? throw new InvalidOperationException("Temperature popup content was not found.");
    }

    private static void ToggleTemperaturePopup(Button temperatureButton)
    {
        temperatureButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static void AssertContainedByPanel(Control control, Border panel)
    {
        Matrix transform = control.TransformToVisual(panel)
            ?? throw new InvalidOperationException("Popup control transform was not found.");
        Rect transformedBounds = new Rect(control.Bounds.Size).TransformToAABB(transform);
        Rect panelBounds = new(panel.Bounds.Size);

        panelBounds.Contains(transformedBounds.TopLeft).Should().BeTrue();
        panelBounds.Contains(transformedBounds.BottomRight).Should().BeTrue();
    }

    private static double GetPromptMinimumHeight(NanoBanana2PanelView view)
    {
        if (view.TryFindResource(PromptHeightResourceKey, out object? value)
            && (value is double promptHeight))
        {
            return promptHeight;
        }

        throw new InvalidOperationException("Prompt height resource was not found.");
    }

    private static Color GetColorResource(Control control, string resourceKey)
    {
        if (control.TryFindResource(resourceKey, out object? value)
            && (value is Color color))
        {
            return color;
        }

        throw new InvalidOperationException($"Color resource '{resourceKey}' was not found.");
    }

    private static IBrush GetBrushResource(Control control, string resourceKey)
    {
        if (control.TryFindResource(resourceKey, out object? value)
            && (value is IBrush brush))
        {
            return brush;
        }

        throw new InvalidOperationException($"Brush resource '{resourceKey}' was not found.");
    }

    private static ImageModelOption GetSelectedModel(UniversalNanoBananaPanelViewModel viewModel)
    {
        return viewModel.SelectedModel
            ?? throw new InvalidOperationException("Selected model is required for this test.");
    }

    private static UniversalNanoBananaPanelViewModel CreateViewModel(
        IGenerationModelCatalogApiClient? catalogApiClient = null,
        IImageModelOptionCatalog? imageModelOptionCatalog = null)
    {
        IImageGenerationApiClient generationApiClient = new SuccessfulImageGenerationApiClient();
        IGenerationLifecycleEventHub generationLifecycleEventHub = new TestGenerationLifecycleEventHub();
        IGenerationRunDispatcher generationRunDispatcher = new GenerationRunDispatcher(
            new GenerationConcurrencyLimiter(),
            generationApiClient,
            new NanoBanana2GenerationLifecyclePublisher(generationLifecycleEventHub),
            new NullGenerationResultStorage(),
            TestGenerationActivityTrackerFactory.Create(),
            NullLogger<GenerationRunDispatcher>.Instance);
        IImageModelOptionCatalog modelOptionCatalog =
            imageModelOptionCatalog ?? CreateLoadedImageModelOptionCatalog();

        return new UniversalNanoBananaPanelViewModel(
            new EmptyFilePickerService(),
            new FixedSecretStore(TestGenerationCredentials.ProviderCredential),
            catalogApiClient
                ?? new FixedGenerationModelCatalogApiClient(
                    ApiModelMetadataTestCatalog.LoadCatalog()),
            modelOptionCatalog,
            TestApiEndpointServiceFactory.Create(),
            new ImmediateUiThreadDispatcher(),
            new UniversalNanoBananaPanelModelScope(),
            new NanoBanana2AttachmentsViewModel(
                new NanoBanana2AttachmentValidator(new AttachedImageSignatureValidator()),
                new PassThroughAttachedImagePreparationService(),
                new NoOpPanelAttachmentStore()),
            new NanoBanana2GenerationRunner(
                new NanoBanana2GenerationRequestBuilder(),
                generationRunDispatcher),
            new NoOpGenerationPanelStateService(),
            new NullImageViewerService(),
            new NanoBanana2QuoteViewModel(new GenerationPricePreviewEstimator()),
            new TestViewModelErrorHandler());
    }

    private static IImageModelOptionCatalog CreateLoadedImageModelOptionCatalog()
    {
        ImageModelOptionCatalog catalog = new();
        catalog.Initialize(ApiModelMetadataTestCatalog.LoadCatalog());

        return catalog;
    }

    private sealed class FixedSecretStore : ISecretStore
    {
        private readonly string _value;

        public FixedSecretStore(string value)
        {
            _value = value;
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken ct)
        {
            return Task.FromResult<string?>(_value);
        }

        public Task SetSecretAsync(string key, string value, CancellationToken ct)
        {
            throw new NotSupportedException("Panel view tests do not write secrets.");
        }
    }

    private sealed class FixedGenerationModelCatalogApiClient : IGenerationModelCatalogApiClient
    {
        public int CallCount { get; private set; }

        private readonly GenerationModelCatalogDto _catalog;

        public FixedGenerationModelCatalogApiClient(GenerationModelCatalogDto catalog)
        {
            _catalog = catalog;
        }

        public Task<GenerationModelCatalogDto> GetCatalogAsync(CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_catalog);
        }
    }

    private sealed class NoOpGenerationPanelStateService : IGenerationPanelStateService
    {
        public Task<GenerationPanelState> LoadAsync(string panelId, CancellationToken ct)
        {
            return Task.FromResult(new GenerationPanelState
            {
                PanelId = panelId
            });
        }

        public Task SaveAsync(string panelId, GenerationPanelState state, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpPanelAttachmentStore : IPanelAttachmentStore
    {
        public PanelAttachmentState CreateState(AttachedImageDto image)
        {
            return new PanelAttachmentState
            {
                Id = "attachment",
                FileName = image.FileName,
                ContentType = image.ContentType,
                SizeBytes = image.Content.LongLength,
                InternalFileName = "attachment"
            };
        }

        public Task SaveAsync(
            string panelId,
            PanelAttachmentState attachment,
            AttachedImageDto image,
            CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<AttachedImageDto?> LoadAsync(
            string panelId,
            PanelAttachmentState attachment,
            CancellationToken ct)
        {
            return Task.FromResult<AttachedImageDto?>(null);
        }

        public Task DeleteAsync(
            string panelId,
            PanelAttachmentState attachment,
            CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
