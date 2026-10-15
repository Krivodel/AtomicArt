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
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.Tests.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Generation;
using static AtomicArt.Desktop.Tests.ViewModels.Generation.UniversalNanoBananaPanelViewModelTestHelper;
using TestGenerationCredentials = AtomicArt.Tests.Common.Generation.TestGenerationCredentials;

namespace AtomicArt.Desktop.Tests.Views.Generation;

public sealed class NanoBanana2PanelViewTests : AnimatedGalleryControlTestBase
{
    private const double ExpandedPanelWidth = 640d;
    private const double ExpandedPanelHeight = 560d;
    private const double WidePanelWidth = 800d;
    private const string PickerAttachmentSource = "picker";
    private const string GalleryAttachmentSource = "gallery";
    private const string DropAttachmentSource = "drop";
    private const string GenerationOptionComboMinWidthResourceKey =
        "GenerationOptionComboMinWidth";
    private const string PromptHeightResourceKey = "PromptHeight";

    [Fact]
    public async Task GenerationOptions_WithDefaultFrameScheduler_WhenPanelWidens_StartsAnimatedTransition()
    {
        await DispatchAsync(() =>
        {
            NanoBanana2PanelView view = CreateView(CreateViewModel());

            Show(view, ExpandedPanelWidth, ExpandedPanelHeight, window =>
            {
                window.Width = 1600d;
                UpdateOptionsLayout(window);

                ContentControl inlineHost = GetInlineOptionsHost(view);
                inlineHost.IsVisible.Should().BeTrue();
                inlineHost.Opacity.Should().BeLessThan(1d);
                inlineHost.IsHitTestVisible.Should().BeFalse();
                GetTemperatureButton(view).IsVisible.Should().BeTrue();
            });
        });
    }

    [Fact]
    public async Task GenerationOptions_WhenPanelWidensAndNarrows_MovesSameControlsAndPreservesValues()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(ExpandedPanelWidth, ExpandedPanelHeight);
            StackPanel options = context.View.FindControl<StackPanel>("GenerationOptionsPanel")
                ?? throw new InvalidOperationException("The generation options panel was not found.");
            ContentControl inlineHost = context.View.FindControl<ContentControl>("InlineGenerationOptionsHost")
                ?? throw new InvalidOperationException("The inline generation options host was not found.");
            Button settingsButton = GetTemperatureButton(context.View);
            Popup popup = OpenTemperaturePopup(context.View);
            Border flyoutPanel = GetTemperaturePopupContent(popup);
            context.ViewModel.Temperature = context.ViewModel.MinimumTemperature;

            context.Window.Width = 1600d;
            RenderOptionsLayout(context);

            settingsButton.IsVisible.Should().BeFalse();
            popup.IsOpen.Should().BeFalse();
            inlineHost.Content.Should().BeSameAs(options);
            options.Orientation.Should().Be(Avalonia.Layout.Orientation.Horizontal);
            options.Bounds.Height.Should().BeLessThanOrEqualTo(GetComboBox(context.View, "GenerationCountComboBox").Bounds.Height);
            context.ViewModel.Temperature.Should().Be(context.ViewModel.MinimumTemperature);

            context.Window.Width = ExpandedPanelWidth;
            RenderOptionsLayout(context);

            settingsButton.IsVisible.Should().BeTrue();
            inlineHost.IsVisible.Should().BeFalse();
            flyoutPanel.Child.Should().BeSameAs(options);
            options.Orientation.Should().Be(Avalonia.Layout.Orientation.Vertical);
            context.ViewModel.Temperature.Should().Be(context.ViewModel.MinimumTemperature);
        });
    }

    [Fact]
    public async Task GenerationOptions_WhenInline_AlignsTemperatureControlsWithParameterRow()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(1600d, ExpandedPanelHeight);
            RenderOptionsLayout(context);
            Slider slider = context.View.FindControl<Slider>("TemperatureSlider")
                ?? throw new InvalidOperationException("The temperature slider was not found.");
            Grid group = slider.Parent.Should().BeOfType<Grid>().Subject;
            TextBlock value = group.Children.OfType<TextBlock>().Single();
            Button icon = GetOptionResetButton(context.View, context.ViewModel.ResetTemperatureCommand);
            Thumb thumb = slider.GetVisualDescendants().OfType<Thumb>()
                .Single(control => control.IsEffectivelyVisible);
            double expectedCenter = GetCenterPoint(GetComboBox(context.View, "ResolutionComboBox"), context.View).Y;
            Control[] controls = [icon, value, slider, thumb];

            foreach (Control control in controls)
            {
                GetCenterPoint(control, context.View).Y.Should().BeApproximately(expectedCenter, 0.5d,
                    "the temperature {0} must align with the parameter row", control.GetType().Name);
            }
        });
    }

    [Fact]
    public async Task GenerationOptions_WhenResizingBackDuringExpansion_ReversesWithoutRestartingPresentation()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(ExpandedPanelWidth, ExpandedPanelHeight);
            ContentControl inlineHost = GetInlineOptionsHost(context.View);
            Button settingsButton = GetTemperatureButton(context.View);
            context.ViewModel.Temperature = context.ViewModel.MinimumTemperature;
            context.Window.Width = 1600d;
            UpdateOptionsLayout(context.Window);
            context.FrameScheduler.RunNextFrame(TimeSpan.Zero);
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(90d));
            double opacityBeforeReversal = inlineHost.Opacity;
            StackPanel options = inlineHost.Content.Should().BeOfType<StackPanel>().Subject;
            double widthBeforeReversal = options.Bounds.Width;

            context.Window.Width = ExpandedPanelWidth;
            UpdateOptionsLayout(context.Window);

            opacityBeforeReversal.Should().BeInRange(0.01d, 0.99d);
            inlineHost.Opacity.Should().Be(opacityBeforeReversal);
            options.Bounds.Width.Should().Be(widthBeforeReversal);
            inlineHost.IsHitTestVisible.Should().BeFalse();
            settingsButton.IsHitTestVisible.Should().BeTrue();

            context.Window.Width = 1600d;
            UpdateOptionsLayout(context.Window);

            inlineHost.Opacity.Should().Be(opacityBeforeReversal);
            settingsButton.IsHitTestVisible.Should().BeFalse();
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(100d));
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(300d));

            inlineHost.IsHitTestVisible.Should().BeTrue();
            inlineHost.Opacity.Should().Be(1d);
            settingsButton.IsVisible.Should().BeFalse();
            context.FrameScheduler.HasQueuedFrame.Should().BeFalse();
            context.ViewModel.Temperature.Should().Be(context.ViewModel.MinimumTemperature);
        });
    }

    [Theory]
    [InlineData(640d, 1600d)]
    [InlineData(1600d, 640d)]
    public async Task GenerationOptions_DuringCrossfade_KeepsControlGeometryAndEnabledAppearanceStable(
        double initialWidth,
        double targetWidth)
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(initialWidth, ExpandedPanelHeight);
            context.Window.Width = targetWidth;
            UpdateOptionsLayout(context.Window);
            ContentControl inlineHost = GetInlineOptionsHost(context.View);
            StackPanel options = inlineHost.Content.Should().BeOfType<StackPanel>().Subject;
            Button settingsButton = GetTemperatureButton(context.View);
            ComboBox countSelector = GetComboBox(context.View, "GenerationCountComboBox");
            Border settingsHost = context.View.FindControl<Border>("GenerationSettingsButtonHost")
                ?? throw new InvalidOperationException("The generation settings button host was not found.");
            double optionsWidth = options.Bounds.Width;
            double optionsHeight = options.Bounds.Height;
            Rect countBounds = countSelector.Bounds;
            context.FrameScheduler.RunNextFrame(TimeSpan.Zero);

            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(90d));
            UpdateOptionsLayout(context.Window);

            inlineHost.Opacity.Should().BeApproximately(0.5d, 0.001d);
            settingsHost.Opacity.Should().BeApproximately(0.5d, 0.001d);
            settingsButton.Opacity.Should().Be(1d);
            settingsHost.Transitions.Should().BeNullOrEmpty();
            inlineHost.Transitions.Should().BeNullOrEmpty();
            inlineHost.IsEnabled.Should().BeTrue();
            settingsButton.IsEnabled.Should().BeTrue();
            options.Bounds.Width.Should().Be(optionsWidth);
            options.Bounds.Height.Should().Be(optionsHeight);
            countSelector.Bounds.Should().Be(countBounds);
            KeyboardNavigation.GetTabNavigation(inlineHost).Should().Be(KeyboardNavigationMode.None);

            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(180d));
            UpdateOptionsLayout(context.Window);

            countSelector.Bounds.Should().Be(countBounds);
            inlineHost.IsHitTestVisible.Should().Be(targetWidth > initialWidth);
            context.FrameScheduler.HasQueuedFrame.Should().BeFalse();
        });
    }

    [Fact]
    public async Task GenerationOptions_WhenCollapsingFocusedSelector_ClosesDropdownAndReturnsFocusToSettings()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(1600d, ExpandedPanelHeight);
            ComboBox thinkingSelector = GetComboBox(context.View, "ThinkingLevelComboBox");
            thinkingSelector.Focus();
            thinkingSelector.IsDropDownOpen = true;

            context.Window.Width = ExpandedPanelWidth;
            UpdateOptionsLayout(context.Window);

            thinkingSelector.IsDropDownOpen.Should().BeFalse();
            GetTemperatureButton(context.View).IsFocused.Should().BeTrue();
            GetInlineOptionsHost(context.View).IsHitTestVisible.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(640d, 1600d)]
    [InlineData(1600d, 640d)]
    public async Task GenerationOptions_WhenMinimizedDuringTransition_SettlesAndDoesNotResumeStaleAnimation(
        double initialWidth,
        double targetWidth)
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(initialWidth, ExpandedPanelHeight);
            context.Window.Width = targetWidth;
            UpdateOptionsLayout(context.Window);
            context.FrameScheduler.RunNextFrame(TimeSpan.Zero);
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(90d));

            context.Window.WindowState = WindowState.Minimized;
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(200d));
            context.Window.WindowState = WindowState.Normal;
            UpdateOptionsLayout(context.Window);

            bool expanded = targetWidth > initialWidth;
            ContentControl inlineHost = GetInlineOptionsHost(context.View);
            inlineHost.IsVisible.Should().Be(expanded);
            inlineHost.IsHitTestVisible.Should().Be(expanded);
            inlineHost.Opacity.Should().Be(expanded ? 1d : 0d);
            GetTemperatureButton(context.View).IsVisible.Should().Be(!expanded);
            context.FrameScheduler.HasQueuedFrame.Should().BeFalse();
        });
    }

    [Fact]
    public async Task GenerationOptions_WhenSettingsClickedDuringCollapse_OpensPopulatedPopupAndFinishesTransition()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(1600d, ExpandedPanelHeight);
            ContentControl inlineHost = GetInlineOptionsHost(context.View);
            object? options = inlineHost.Content;
            context.Window.Width = ExpandedPanelWidth;
            UpdateOptionsLayout(context.Window);
            context.FrameScheduler.RunNextFrame(TimeSpan.Zero);

            Popup popup = OpenTemperaturePopup(context.View);
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(200d));

            popup.IsOpen.Should().BeTrue();
            GetTemperaturePopupContent(popup).Child.Should().BeSameAs(options);
            inlineHost.IsVisible.Should().BeFalse();
            context.FrameScheduler.HasQueuedFrame.Should().BeFalse();
        });
    }

    [Fact]
    public async Task GenerationOptions_WhenDetachedDuringExpansion_CancelsAndReattachesWithoutStaleFrames()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(ExpandedPanelWidth, ExpandedPanelHeight);
            context.Window.Width = 1600d;
            UpdateOptionsLayout(context.Window);
            context.FrameScheduler.RunNextFrame(TimeSpan.Zero);
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(90d));

            context.Window.Content = null;
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(200d));
            context.Window.Width = ExpandedPanelWidth;
            context.Window.Content = context.View;
            UpdateOptionsLayout(context.Window);

            GetInlineOptionsHost(context.View).IsVisible.Should().BeFalse();
            GetTemperatureButton(context.View).IsVisible.Should().BeTrue();
            GetTemperatureButton(context.View).Opacity.Should().Be(1d);
            context.FrameScheduler.HasQueuedFrame.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(640d, 1600d)]
    [InlineData(1600d, 640d)]
    public async Task GenerationOptions_WhenModelChangesDuringResize_SettlesImmediatelyAndKeepsSelectedQuality(
        double initialWidth,
        double targetWidth)
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(initialWidth, ExpandedPanelHeight);
            context.Window.Width = targetWidth;
            UpdateOptionsLayout(context.Window);
            context.FrameScheduler.RunNextFrame(TimeSpan.Zero);
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(90d));

            context.ViewModel.SelectedModel = context.ViewModel.AvailableModels.Single(model =>
                model.Id == "openrouter-gpt-image-2-5-sunburst");
            context.ViewModel.SelectedQualityOption = context.ViewModel.QualityLevels.Last();
            GenerationOptionViewModel? selectedQuality = context.ViewModel.SelectedQualityOption;
            UpdateOptionsLayout(context.Window);

            bool expanded = targetWidth > initialWidth;
            GetInlineOptionsHost(context.View).IsHitTestVisible.Should().Be(expanded);
            GetInlineOptionsHost(context.View).Opacity.Should().Be(expanded ? 1d : 0d);
            GetTemperatureButton(context.View).IsVisible.Should().Be(!expanded);
            context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(200d));

            GetInlineOptionsHost(context.View).IsHitTestVisible.Should().Be(expanded);
            GetTemperatureButton(context.View).IsVisible.Should().Be(!expanded);
            context.ViewModel.SelectedQualityOption.Should().BeSameAs(selectedQuality);
            context.FrameScheduler.HasQueuedFrame.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(640d)]
    [InlineData(800d)]
    [InlineData(900d)]
    [InlineData(1000d)]
    [InlineData(1100d)]
    [InlineData(1600d)]
    public async Task GenerationOptions_WhenModelChanges_SettlesWithoutRequestingAnimationFrames(double panelWidth)
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(panelWidth, ExpandedPanelHeight);
            RenderOptionsLayout(context);
            ImageModelOption originalModel = context.ViewModel.SelectedModel
                ?? throw new InvalidOperationException("The initial model was not selected.");
            ImageModelOption qualityModel = context.ViewModel.AvailableModels.Single(model =>
                model.Id == "openrouter-gpt-image-2-5-sunburst");
            int frameCount = context.FrameScheduler.RequestedFrameCount;
            ImageModelOption[] models = [qualityModel, originalModel];

            foreach (ImageModelOption model in models)
            {
                context.ViewModel.SelectedModel = model;
                UpdateOptionsLayout(context.Window);

                ContentControl inlineHost = GetInlineOptionsHost(context.View);
                Button settingsButton = GetTemperatureButton(context.View);
                context.FrameScheduler.RequestedFrameCount.Should().Be(frameCount);
                context.FrameScheduler.HasQueuedFrame.Should().BeFalse();
                inlineHost.Opacity.Should().Be(inlineHost.IsVisible ? 1d : 0d);
                inlineHost.IsHitTestVisible.Should().Be(inlineHost.IsVisible);
                settingsButton.IsVisible.Should().Be(!inlineHost.IsVisible);

                if (panelWidth == 640d || panelWidth == 1600d)
                {
                    inlineHost.IsVisible.Should().Be(panelWidth == 1600d);
                }
            }
        });
    }

    [Theory]
    [InlineData(640d, false, false)]
    [InlineData(1000d, true, false)]
    [InlineData(1600d, true, true)]
    public async Task GenerationOptions_WhenModelSelectorChanges_UsesFinalPresentationInFirstLayoutPass(
        double panelWidth,
        bool qualityInline,
        bool temperatureInline)
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(panelWidth, ExpandedPanelHeight);
            RenderOptionsLayout(context);
            ImageModelOption originalModel = context.ViewModel.SelectedModel
                ?? throw new InvalidOperationException("The initial model was not selected.");
            ImageModelOption qualityModel = context.ViewModel.AvailableModels.Single(model =>
                model.Id == "openrouter-gpt-image-2-5-sunburst");
            Grid row = context.View.FindControl<Grid>("GenerationParametersRow")
                ?? throw new InvalidOperationException("The generation parameters row was not found.");
            ComboBox modelSelector = row.Children.OfType<ComboBox>().First();
            ContentControl inlineHost = GetInlineOptionsHost(context.View);
            Button settingsButton = GetTemperatureButton(context.View);

            modelSelector.SelectedItem = qualityModel;
            context.Window.UpdateLayout();

            inlineHost.IsVisible.Should().Be(qualityInline);
            inlineHost.Opacity.Should().Be(qualityInline ? 1d : 0d);
            inlineHost.IsHitTestVisible.Should().Be(qualityInline);
            settingsButton.IsVisible.Should().Be(!qualityInline);

            modelSelector.SelectedItem = originalModel;
            context.Window.UpdateLayout();

            inlineHost.IsVisible.Should().Be(temperatureInline);
            inlineHost.Opacity.Should().Be(temperatureInline ? 1d : 0d);
            inlineHost.IsHitTestVisible.Should().Be(temperatureInline);
            settingsButton.IsVisible.Should().Be(!temperatureInline);
            context.FrameScheduler.HasQueuedFrame.Should().BeFalse();
        });
    }

    [Fact]
    public async Task GenerationOptions_WhenWidePanelChangesModel_UpdatesInlineParametersAndRetainsQuality()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(1600d, ExpandedPanelHeight);
            ImageModelOption gptModel = context.ViewModel.AvailableModels.Single(model =>
                model.Id == "openrouter-gpt-image-2-5-sunburst");

            context.ViewModel.SelectedModel = gptModel;
            RenderOptionsLayout(context);

            ContentControl inlineHost = context.View.FindControl<ContentControl>("InlineGenerationOptionsHost")
                ?? throw new InvalidOperationException("The inline generation options host was not found.");
            StackPanel options = inlineHost.Content.Should().BeOfType<StackPanel>().Subject;
            ComboBox qualitySelector = options.GetLogicalDescendants().OfType<ComboBox>()
                .Single(comboBox => ReferenceEquals(comboBox.ItemsSource, context.ViewModel.QualityLevels));
            GetTemperatureButton(context.View).IsVisible.Should().BeFalse();
            qualitySelector.IsEffectivelyVisible.Should().BeTrue();
            qualitySelector.SelectedItem = context.ViewModel.QualityLevels.Last();
            GenerationOptionViewModel selectedQuality = context.ViewModel.SelectedQualityOption
                ?? throw new InvalidOperationException("The selected quality was not set.");

            context.Window.Width = ExpandedPanelWidth;
            RenderOptionsLayout(context);

            context.ViewModel.SelectedQualityOption.Should().BeSameAs(selectedQuality);
            GetTemperatureButton(context.View).IsVisible.Should().BeTrue();
        });
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(GenerationAspectRatios.Auto, false)]
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
    public void GetAspectRatioHintValue_WithLocalizedOption_ReturnsMachineValue()
    {
        GenerationOptionViewModel option = new(
            "16:9",
            null,
            TestLocalizationTextProvider.Default);

        string? result = NanoBanana2PanelView.GetAspectRatioHintValue(option);

        result.Should().Be("16:9");
    }

    [Fact]
    public async Task PromptTextBox_WhenPanelIsShown_ReceivesFocus()
    {
        await DispatchAsync(async () =>
        {
            NanoBanana2PanelView view = CreateView(
                out UniversalNanoBananaPanelViewModel _);
            Window window = Show(view);

            try
            {
                await view.Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Background);

                GetPromptTextBox(view).IsFocused.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(PickerAttachmentSource)]
    [InlineData(GalleryAttachmentSource)]
    [InlineData(DropAttachmentSource)]
    public async Task AttachedImages_WhenImageIsAddedFromSource_FocusesPromptInput(
        string source)
    {
        await DispatchAsync(async () =>
        {
            AttachedImageDto image = new(
                "attached.png",
                GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(2, 2));
            IFilePickerService filePickerService = string.Equals(
                source,
                PickerAttachmentSource,
                StringComparison.Ordinal)
                ? new FixedFilePickerService(image)
                : new EmptyFilePickerService();
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
                filePickerService: filePickerService);
            NanoBanana2PanelView view = CreateView(viewModel);
            Window window = Show(view);

            try
            {
                await view.Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Background);

                TextBox promptTextBox = GetPromptTextBox(view);
                Button attachButton = view
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => button.Command == viewModel.PickImageCommand);
                attachButton.Focus().Should().BeTrue();
                promptTextBox.IsFocused.Should().BeFalse();

                await AttachImageFromSourceAsync(viewModel, image, source);
                await view.Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Background);

                promptTextBox.IsFocused.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task SelectionValueReset_WhenResolutionResets_FlashesTemplateBackgroundThenFadesAndRestoresOriginalValue()
    {
        await DispatchAsync(async () =>
        {
            NanoBanana2PanelView view = CreateView(
                out UniversalNanoBananaPanelViewModel viewModel);
            string unsupportedResolution = GetSelectedModel(viewModel).Resolutions.Last();
            ImageModelOption modelWithoutResolution = viewModel.AvailableModels.First(model =>
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
    public async Task PromptTextBox_WhenPanelIsTallerThanMinimum_FillsAvailableHeight()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(ExpandedPanelWidth, ExpandedPanelHeight);
            TextBox promptTextBox = context.View
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single();
            double promptMinimumHeight = GetPromptMinimumHeight(context.View);

            promptTextBox.MinHeight.Should().Be(promptMinimumHeight);
            promptTextBox.Bounds.Height.Should().BeGreaterThan(promptMinimumHeight);
        });
    }

    [Fact]
    public async Task GenerationCountComboBox_WhenPanelNarrows_ShrinksFromPreferredWidthAndKeepsRightAlignment()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(WidePanelWidth, ExpandedPanelHeight);
            Grid parameterRow = context.View.FindControl<Grid>("GenerationParametersRow")
                ?? throw new InvalidOperationException("The generation parameter row was not found.");

            ComboBox generationCountComboBox = GetComboBox(
                context.View,
                "GenerationCountComboBox");
            double preferredWidth = GetDoubleResource(
                context.View,
                GenerationOptionComboMinWidthResourceKey);
            double preferredRightGap = GetRightGap(generationCountComboBox, context.View);
            double initialWidth = generationCountComboBox.Bounds.Width;

            initialWidth.Should().BePositive().And.BeLessThanOrEqualTo(preferredWidth);
            preferredRightGap.Should().BeApproximately(
                generationCountComboBox.Margin.Right,
                0.01d);

            context.Window.Width = parameterRow.ColumnDefinitions
                .Take(Grid.GetColumn(generationCountComboBox))
                .Sum(column => column.ActualWidth)
                + initialWidth / 2d
                + generationCountComboBox.Margin.Left
                + generationCountComboBox.Margin.Right;
            context.Window.CaptureRenderedFrame();

            generationCountComboBox.Bounds.Width.Should().BeLessThan(initialWidth);
            GetRightGap(generationCountComboBox, context.View)
                .Should()
                .BeApproximately(preferredRightGap, 0.01d);
        });
    }

    [Fact]
    public async Task Panel_WhenShownBeforeStateRestore_DoesNotLoadModelCatalog()
    {
        await DispatchAsync(() =>
        {
            FixedGenerationModelCatalogApiClient catalogApiClient = new(
                ApiModelMetadataTestCatalog.LoadCatalog());
            ImageModelOptionCatalog imageModelOptionCatalog = new();
            UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
                catalogApiClient,
                imageModelOptionCatalog);
            NanoBanana2PanelView view = CreateView(viewModel);
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
    public async Task PromptTextBox_WhenShown_HasHalfSpeedWheelScrolling()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            ScrollViewer promptScrollViewer = context.View
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
        });
    }

    [Fact]
    public async Task PromptTextBox_WithDefaultSetting_PreservesThemeTextSize()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            TextBox promptTextBox = context.View
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single();
            TextBox themeTextBox = new()
            {
                Classes = { "prompt-input" }
            };
            Window themeWindow = Show(themeTextBox);

            try
            {
                promptTextBox.FontSize.Should().Be(themeTextBox.FontSize);
            }
            finally
            {
                themeWindow.Close();
            }
        });
    }

    [Fact]
    public async Task PromptTextBox_WhenControlWheelMovesUp_IncreasesBoundTextSize()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            TextBox promptTextBox = context.View
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single();
            double initialTextSize = context.ViewModel.PromptTextSize;
            Point pointerPosition = GetCenterPoint(promptTextBox, context.Window);

            context.Window.MouseWheel(
                pointerPosition,
                new Vector(0d, 1d),
                RawInputModifiers.Control);

            context.ViewModel.PromptTextSize.Should().Be(initialTextSize + 1d);
            promptTextBox.FontSize.Should().Be(initialTextSize + 1d);
        });
    }

    [Fact]
    public async Task PromptPlaceholder_WhenControlWheelMovesUp_MatchesPromptTextSize()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            TextBox promptTextBox = context.View
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single();
            TextBlock placeholder = promptTextBox
                .GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(textBlock => string.Equals(
                    textBlock.Name,
                    "PART_Placeholder",
                    StringComparison.Ordinal));
            double initialTextSize = context.ViewModel.PromptTextSize;
            Point pointerPosition = GetCenterPoint(promptTextBox, context.Window);

            context.Window.MouseWheel(
                pointerPosition,
                new Vector(0d, 1d),
                RawInputModifiers.Control);

            placeholder.FontSize.Should().Be(initialTextSize + 1d);
        });
    }

    [Fact]
    public async Task PromptTextBox_WhenWheelMovesWithoutControl_KeepsBoundTextSize()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            TextBox promptTextBox = context.View
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single();
            double initialTextSize = context.ViewModel.PromptTextSize;
            Point pointerPosition = GetCenterPoint(promptTextBox, context.Window);

            context.Window.MouseWheel(
                pointerPosition,
                new Vector(0d, 1d),
                RawInputModifiers.None);

            context.ViewModel.PromptTextSize.Should().Be(initialTextSize);
            promptTextBox.FontSize.Should().Be(initialTextSize);
        });
    }

    [Fact]
    public async Task AttachImageButton_WhenPanelIsShown_CentersVectorIcon()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            Button button = context.View
                .GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Command == context.ViewModel.PickImageCommand);
            PathIcon icon = button.Content.Should()
                .BeOfType<PathIcon>()
                .Subject;
            button.TryFindResource("AppAddIcon", out object? addGeometry).Should().BeTrue();
            Matrix transform = icon.TransformToVisual(button)
                ?? throw new InvalidOperationException("Attachment icon transform was not found.");
            Rect iconBounds = new Rect(icon.Bounds.Size).TransformToAABB(transform);
            double pixelTolerance = 1d / context.Window.RenderScaling;

            icon.Data.Should().BeSameAs(addGeometry);
            iconBounds.Center.X.Should().BeApproximately(button.Bounds.Width / 2d, pixelTolerance);
            iconBounds.Center.Y.Should().BeApproximately(button.Bounds.Height / 2d, pixelTolerance);
        });
    }

    [Fact]
    public async Task TemperatureButton_WhenPanelIsShown_ContainsCatalogConfiguredSliderAndResetButton()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            Button temperatureButton = GetTemperatureButton(context.View);
            Popup popup = GetTemperaturePopup(context.View);

            popup.IsOpen = true;

            Border content = GetTemperaturePopupContent(popup);
            Slider slider = content
                .GetLogicalDescendants()
                .OfType<Slider>()
                .Single();
            Button resetButton = GetOptionResetButton(context.View, context.ViewModel.ResetTemperatureCommand);

            slider.Minimum.Should().Be(context.ViewModel.MinimumTemperature);
            slider.Maximum.Should().Be(context.ViewModel.MaximumTemperature);
            slider.TickFrequency.Should().Be(context.ViewModel.TemperatureStep);
            slider.Value.Should().Be(context.ViewModel.DefaultTemperature);
            resetButton.Command.Should().BeSameAs(context.ViewModel.ResetTemperatureCommand);

            popup.IsOpen = false;
        });
    }

    [Fact]
    public async Task SettingsFlyout_WhenPanelIsShown_UsesIconsAndCatalogThinkingLevels()
    {
        await DispatchAsync(() =>
        {
            NanoBanana2PanelView view = CreateView(
                out UniversalNanoBananaPanelViewModel viewModel);
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
                window.CaptureRenderedFrame();

                Border content = GetTemperaturePopupContent(popup);
                ThemeVariantScope themeScope = popup.Child.Should()
                    .BeOfType<ThemeVariantScope>()
                    .Subject;
                Color textColor = GetColorResource(content, "SukiText");
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
                Button thinkingResetButton = GetOptionResetButton(view, viewModel.ResetThinkingLevelCommand);
                Grid thinkingControls = thinkingComboBox.Parent.Should()
                    .BeOfType<Grid>()
                    .Subject;

                temperatureValue.Bounds.Top.Should().BeLessThan(slider.Bounds.Top);
                themeScope.ActualThemeVariant.Should().Be(ThemeVariant.Dark);
                temperatureValue.Foreground.Should().BeAssignableTo<ISolidColorBrush>()
                    .Which.Color.Should().Be(textColor);
                temperatureIcon.Foreground.Should().BeAssignableTo<ISolidColorBrush>()
                    .Which.Color.Should().Be(textColor);
                content.GetLogicalDescendants().OfType<TextBlock>()
                    .Select(textBlock => textBlock.Text)
                    .OfType<string>()
                    .Should()
                    .NotContain(text => text.Contains("Температура", StringComparison.Ordinal));
                thinkingLevels.Select(level => level.LocalizationKey).Should().Equal(
                    GenerationLocalizationKeys.ThinkingLow,
                    GenerationLocalizationKeys.ThinkingHigh);
                slider.Cursor?.ToString().Should().Be(nameof(StandardCursorType.Hand));
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
    public async Task TemperatureButton_WhenPanelIsShown_UsesConfiguredSquareSizeAndLargerIcon()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            Button temperatureButton = GetTemperatureButton(context.View);
            double configuredSize = GetDoubleResource(context.View, "GenerationOptionButtonSize");
            PathIcon temperatureIcon = temperatureButton
                .GetVisualDescendants()
                .OfType<PathIcon>()
                .Single();

            temperatureButton.Bounds.Height.Should().Be(configuredSize);
            temperatureButton.Bounds.Width.Should().Be(temperatureButton.Bounds.Height);
            temperatureIcon.Bounds.Width.Should().BeGreaterThan(16d);
        });
    }

    [Fact]
    public async Task TemperatureControls_WhenPanelIsShown_HaveNoToolTips()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            Button temperatureButton = GetTemperatureButton(context.View);
            Popup popup = GetTemperaturePopup(context.View);
            Border content = GetTemperaturePopupContent(popup);
            IReadOnlyList<Button> resetButtons = content
                .GetLogicalDescendants()
                .OfType<Button>()
                .ToList();

            ToolTip.GetTip(temperatureButton).Should().BeNull();
            resetButtons.Should().AllSatisfy(button =>
                ToolTip.GetTip(button).Should().BeNull());
        });
    }

    [Fact]
    public async Task PopupThemeResources_WhenLoaded_AreOpaqueBlue()
    {
        await DispatchAsync(() =>
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
    public async Task TemperaturePopup_WhenPanelIsScaled_InheritsTransformWithoutClipping(
        double uiScale)
    {
        await DispatchAsync(() =>
        {
            NanoBanana2PanelView view = CreateView(
                out UniversalNanoBananaPanelViewModel viewModel);
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
                Button temperatureResetButton = GetOptionResetButton(view, viewModel.ResetTemperatureCommand);
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

                PopupAssertions.AssertInheritsScale(popup, panel, uiScale);
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
            NanoBanana2PanelView view = CreateView(
                out UniversalNanoBananaPanelViewModel viewModel);
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
                AssertTemperaturePanelAnimating(
                    panel,
                    motionScale,
                    motionTranslate);

                await Task.Delay(TimeSpan.FromMilliseconds(100d));

                panel.Opacity.Should().Be(1d);
                motionScale.ScaleX.Should().Be(1d);
                motionScale.ScaleY.Should().Be(1d);
                motionTranslate.Y.Should().Be(0d);

                ToggleTemperaturePopup(temperatureButton);

                popup.IsOpen.Should().BeTrue();

                await Task.Delay(TimeSpan.FromMilliseconds(75d));

                AssertTemperaturePanelAnimating(
                    panel,
                    motionScale,
                    motionTranslate);

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
            using ShownPanelContext context = new();
            context.ViewModel.SelectedModel = context.ViewModel.AvailableModels.Single(model =>
                model.Id == ApiModelMetadataTestCatalog.NanoBanana2ModelId);
            Popup settingsPopup = OpenTemperaturePopup(context.View);

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
        });
    }

    [Theory]
    [InlineData("openrouter-gpt-image-2", "Medium")]
    [InlineData("openrouter-gpt-image-2-5-sunburst", "Xhigh")]
    [InlineData("openrouter-gpt-image-2-5-sunburst", "Max")]
    [InlineData("openrouter-gpt-image-2-5-flare", "Xhigh")]
    [InlineData("openrouter-gpt-image-2-5-flare", "Max")]
    public async Task QualityDropDown_WhenSelectionChanges_UpdatesViewModel(
        string modelId,
        string quality)
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            context.ViewModel.SelectedModel = context.ViewModel.AvailableModels.Single(model =>
                model.Id == modelId);
            Popup settingsPopup = OpenTemperaturePopup(context.View);
            Border settingsPanel = GetTemperaturePopupContent(settingsPopup);
            ComboBox qualityComboBox = settingsPanel
                .GetLogicalDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => ReferenceEquals(
                    comboBox.ItemsSource,
                    context.ViewModel.QualityLevels));
            qualityComboBox.SelectedItem = context.ViewModel.QualityLevels
                .Single(level => level.Value == quality);

            context.ViewModel.SelectedQuality.Should().Be(quality);
        });
    }

    [Theory]
    [InlineData("openrouter-gpt-image-2")]
    [InlineData("openrouter-gpt-image-2-5-sunburst")]
    [InlineData("openrouter-gpt-image-2-5-flare")]
    public async Task GenerationOptions_WhenSwitchingBetweenNanoBanana21AndGptImage_ReflectsSelectedModel(
        string gptModelId)
    {
        await DispatchAsync(() =>
        {
            using UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
            ImageModelOption nanoBananaModel = viewModel.AvailableModels.Single(model =>
                model.Id == ApiModelMetadataTestCatalog.OpenRouterNanoBanana21ModelId);
            ImageModelOption gptModel = viewModel.AvailableModels.Single(model =>
                model.Id == gptModelId);
            viewModel.SelectedModel = nanoBananaModel;
            NanoBanana2PanelView view = CreateView(viewModel);
            Button settingsButton = view.FindControl<Button>("TemperatureButton")
                ?? throw new InvalidOperationException("The generation options button should be available.");
            Popup settingsPopup = view.FindControl<Popup>("TemperaturePopup")
                ?? throw new InvalidOperationException("The generation options popup should be available.");
            ComboBox qualityComboBox = GetTemperaturePopupContent(settingsPopup)
                .GetLogicalDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => ReferenceEquals(comboBox.ItemsSource, viewModel.QualityLevels));
            Control qualityRow = qualityComboBox.Parent as Control
                ?? throw new InvalidOperationException("The quality row should be available.");
            settingsButton.IsVisible.Should().BeFalse();
            qualityRow.IsVisible.Should().BeFalse();

            viewModel.SelectedModel = gptModel;

            settingsButton.IsVisible.Should().BeTrue();
            qualityRow.IsVisible.Should().BeTrue();
            qualityComboBox.Items.Should().HaveCount(viewModel.QualityLevels.Count);
            qualityComboBox.SelectedItem.Should().BeSameAs(viewModel.SelectedQualityOption);

            viewModel.SelectedModel = nanoBananaModel;

            settingsButton.IsVisible.Should().BeFalse();
            qualityRow.IsVisible.Should().BeFalse();
            qualityComboBox.Items.Should().BeEmpty();
            ComboBox aspectRatioComboBox = view.FindControl<ComboBox>("AspectRatioComboBox")
                ?? throw new InvalidOperationException("The aspect ratio dropdown should be available.");
            ComboBox resolutionComboBox = view.FindControl<ComboBox>("ResolutionComboBox")
                ?? throw new InvalidOperationException("The resolution dropdown should be available.");
            aspectRatioComboBox.Items.Should().HaveCount(nanoBananaModel.AspectRatios.Count);
            aspectRatioComboBox.SelectedItem.Should().NotBeNull();
            resolutionComboBox.Items.Should().HaveCount(nanoBananaModel.Resolutions.Count);
            resolutionComboBox.SelectedItem.Should().Be(viewModel.SelectedResolution);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task GenerationOptions_WhenRendered_HasOnlyLeftResetIcons()
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            Popup popup = OpenTemperaturePopup(context.View);
            Border panel = GetTemperaturePopupContent(popup);
            Button[] resetButtons = panel.GetLogicalDescendants().OfType<Button>().ToArray();

            resetButtons.Should().HaveCount(3);
            resetButtons.Should().AllSatisfy(button =>
            {
                Grid.GetColumn(button).Should().Be(0);
                button.Content.Should().BeOfType<PathIcon>();
                button.Classes.Should().Contain("generation-option-reset");
                ToolTip.GetTip(button).Should().BeNull();
            });
        });
    }

    [Theory]
    [InlineData(640d)]
    [InlineData(1600d)]
    public async Task GenerationOptionIcons_WhenClicked_ResetOnlyTheirParameter(double panelWidth)
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(panelWidth, ExpandedPanelHeight);
            UniversalNanoBananaPanelViewModel viewModel = context.ViewModel;
            GenerationModelThinkingLevelMetadataDto? defaultThinking = viewModel.SelectedThinkingLevel;
            viewModel.Temperature = viewModel.MaximumTemperature;
            viewModel.SelectedThinkingLevel = viewModel.ThinkingLevels.First(level => !ReferenceEquals(level, defaultThinking));
            GenerationModelThinkingLevelMetadataDto? changedThinking = viewModel.SelectedThinkingLevel;
            ShowGenerationOptions(context);

            ClickOptionResetButton(GetOptionResetButton(context.View, viewModel.ResetTemperatureCommand));

            viewModel.Temperature.Should().Be(viewModel.DefaultTemperature);
            viewModel.SelectedThinkingLevel.Should().BeSameAs(changedThinking);
            viewModel.Temperature = viewModel.MinimumTemperature;

            ClickOptionResetButton(GetOptionResetButton(context.View, viewModel.ResetThinkingLevelCommand));

            viewModel.SelectedThinkingLevel.Should().BeSameAs(defaultThinking);
            viewModel.Temperature.Should().Be(viewModel.MinimumTemperature);
            viewModel.SelectedModel = viewModel.AvailableModels.Single(model =>
                model.Id == "openrouter-gpt-image-2-5-sunburst");
            viewModel.SelectedQuality = "Low";
            ShowGenerationOptions(context);

            ClickOptionResetButton(GetOptionResetButton(context.View, viewModel.ResetQualityCommand));

            viewModel.SelectedQuality.Should().Be("Max");
            viewModel.SelectedQualityOption?.Value.Should().Be("Max");
        });
    }

    [Theory]
    [InlineData(640d)]
    [InlineData(1600d)]
    public async Task GenerationOptionIcons_WhenHovered_AnimateForegroundToRedAndBack(double panelWidth)
    {
        await DispatchAsync(async () =>
        {
            using ShownPanelContext context = new(panelWidth, ExpandedPanelHeight);
            ShowGenerationOptions(context);
            Popup popup = GetTemperaturePopup(context.View);

            if (popup.IsOpen)
            {
                Transitions transitions = GetTemperaturePopupContent(popup).Transitions
                    ?? throw new InvalidOperationException("The options popup animation was not configured.");
                TimeSpan openDuration = transitions.OfType<DoubleTransition>().Single().Duration;
                await Task.Delay(openDuration + openDuration);
                context.Window.CaptureRenderedFrame();
            }

            Button button = GetOptionResetButton(context.View, context.ViewModel.ResetTemperatureCommand);
            TopLevel root = TopLevel.GetTopLevel(button)
                ?? throw new InvalidOperationException("The reset icon was not attached to a window.");
            PathIcon icon = button.Content.Should().BeOfType<PathIcon>().Subject;
            Color normalColor = button.Foreground.Should().BeAssignableTo<ISolidColorBrush>().Which.Color;
            Color hoverColor = GetColorResource(button, "SukiDangerColor");
            BrushTransition transition = button.Transitions.Should().ContainSingle().Which
                .Should().BeOfType<BrushTransition>().Subject;
            transition.Property.Should().Be(Button.ForegroundProperty);
            Point center = GetCenterPoint(button, root);

            root.MouseMove(center);
            root.CaptureRenderedFrame();
            await Task.Delay(transition.Duration / 3d);
            root.CaptureRenderedFrame();

            Color intermediateColor = icon.Foreground.Should().BeAssignableTo<ISolidColorBrush>().Which.Color;
            intermediateColor.Should().NotBe(normalColor).And.NotBe(hoverColor);
            await Task.Delay(transition.Duration);
            root.CaptureRenderedFrame();

            icon.Foreground.Should().BeAssignableTo<ISolidColorBrush>().Which.Color.Should().Be(hoverColor);
            root.MouseMove(new Point(root.Bounds.Width - 1d, root.Bounds.Height - 1d));
            root.CaptureRenderedFrame();
            await Task.Delay(transition.Duration + transition.Duration);
            root.CaptureRenderedFrame();

            icon.Foreground.Should().BeAssignableTo<ISolidColorBrush>().Which.Color.Should().Be(normalColor);
        });
    }

    [Theory]
    [InlineData("openrouter-gpt-image-2", "High")]
    [InlineData("openrouter-gpt-image-2-5-sunburst", "Max")]
    [InlineData("openrouter-gpt-image-2-5-flare", "Max")]
    public async Task QualityDropDown_WhenGptModelIsSelected_ShowsMaximumWithoutAutomaticOption(
        string modelId, string expectedQuality)
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new();
            context.ViewModel.SelectedModel = context.ViewModel.AvailableModels.Single(model =>
                model.Id == modelId);
            Popup settingsPopup = OpenTemperaturePopup(context.View);
            Border settingsPanel = GetTemperaturePopupContent(settingsPopup);
            ComboBox qualityComboBox = settingsPanel
                .GetLogicalDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => ReferenceEquals(
                    comboBox.ItemsSource,
                    context.ViewModel.QualityLevels));

            context.ViewModel.SelectedQuality.Should().Be(expectedQuality);
            qualityComboBox.SelectedItem.Should().BeSameAs(context.ViewModel.SelectedQualityOption);
            qualityComboBox.Items.OfType<GenerationOptionViewModel>()
                .Should().NotContain(option => option.Value == "Auto");
        });
    }

    [Theory]
    [InlineData("openrouter-gpt-image-2", 640d)]
    [InlineData("openrouter-gpt-image-2", 1600d)]
    [InlineData("openrouter-gpt-image-2-5-sunburst", 640d)]
    [InlineData("openrouter-gpt-image-2-5-sunburst", 1600d)]
    [InlineData("openrouter-gpt-image-2-5-flare", 640d)]
    [InlineData("openrouter-gpt-image-2-5-flare", 1600d)]
    public async Task QualityDropDown_WhenGptQualityChanges_ColorsSelectedValueAndItems(
        string modelId, double panelWidth)
    {
        await DispatchAsync(() =>
        {
            using ShownPanelContext context = new(panelWidth);
            context.ViewModel.SelectedModel = context.ViewModel.AvailableModels.Single(model =>
                model.Id == modelId);
            ShowGenerationOptions(context);
            StackPanel optionsPanel = context.View.FindControl<StackPanel>("GenerationOptionsPanel")
                ?? throw new InvalidOperationException("The generation options panel was not found.");
            ComboBox qualityComboBox = optionsPanel.GetLogicalDescendants().OfType<ComboBox>()
                .Single(comboBox => ReferenceEquals(comboBox.ItemsSource, context.ViewModel.QualityLevels));
            Dictionary<string, string> expectedColors = new()
            {
                ["Low"] = "#FF0000",
                ["Medium"] = "#FF8040",
                ["High"] = "#00FF40",
                ["Xhigh"] = "#56F8C7",
                ["Max"] = "#ECB0FF"
            };

            foreach (GenerationOptionViewModel option in context.ViewModel.QualityLevels)
            {
                qualityComboBox.SelectedItem = option;
                UpdateOptionsLayout(context.Window);

                TextBlock selectedText = qualityComboBox.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("quality-option"));
                selectedText.Foreground.Should().BeAssignableTo<ISolidColorBrush>()
                    .Which.Color.Should().Be(Color.Parse(expectedColors[option.Value]));
            }

            qualityComboBox.IsDropDownOpen = true;
            UpdateOptionsLayout(context.Window);
            Popup dropDown = qualityComboBox.GetVisualDescendants().OfType<Popup>()
                .Single(popup => popup.Name == "PART_Popup");
            Control dropDownContent = dropDown.Child
                ?? throw new InvalidOperationException("The quality dropdown content was not found.");
            TextBlock[] itemTexts = dropDownContent.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.Classes.Contains("quality-option"))
                .ToArray();

            itemTexts.Should().HaveCount(context.ViewModel.QualityLevels.Count);
            foreach (TextBlock itemText in itemTexts)
            {
                GenerationOptionViewModel option = itemText.DataContext
                    .Should().BeOfType<GenerationOptionViewModel>().Subject;
                itemText.Foreground.Should().BeAssignableTo<ISolidColorBrush>()
                    .Which.Color.Should().Be(Color.Parse(expectedColors[option.Value]));
            }

            qualityComboBox.IsDropDownOpen = false;
            context.ViewModel.SelectedModel = context.ViewModel.AvailableModels.Single(model =>
                model.Id == ApiModelMetadataTestCatalog.NanoBanana2ModelId);
            ShowGenerationOptions(context);

            qualityComboBox.Classes.Should().NotContain("gpt-quality");
        });
    }

    [Fact]
    public async Task SettingsPopup_WhenOutsideIsClicked_ClosesAfterAnimation()
    {
        await DispatchAsync(async () =>
        {
            using ShownPanelContext context = new();
            Popup settingsPopup = OpenTemperaturePopup(context.View);

            Point outsidePoint = new(
                context.Window.Bounds.Width - 1d,
                context.Window.Bounds.Height - 1d);
            context.Window.MouseDown(outsidePoint, MouseButton.Left);
            context.Window.MouseUp(outsidePoint, MouseButton.Left);
            await Task.Delay(TimeSpan.FromMilliseconds(200d));

            settingsPopup.IsOpen.Should().BeFalse();
        });
    }

    private static NanoBanana2PanelView CreateView(
        out UniversalNanoBananaPanelViewModel viewModel)
    {
        viewModel = CreateViewModel();

        return CreateView(viewModel);
    }

    private static NanoBanana2PanelView CreateView(
        UniversalNanoBananaPanelViewModel viewModel)
    {
        return new NanoBanana2PanelView
        {
            DataContext = viewModel
        };
    }

    private static UniversalNanoBananaPanelViewModel CreateViewModel(
        IGenerationModelCatalogApiClient? catalogApiClient = null,
        IImageModelOptionCatalog? imageModelOptionCatalog = null,
        IFilePickerService? filePickerService = null)
    {
        IImageGenerationApiClient generationApiClient = new SuccessfulImageGenerationApiClient();
        IGenerationLifecycleEventHub generationLifecycleEventHub = new TestGenerationLifecycleEventHub();
        IGenerationRunDispatcher generationRunDispatcher =
            GenerationRunDispatcherTestFactory.Create(
                generationApiClient,
                generationLifecycleEventHub);
        IImageModelOptionCatalog modelOptionCatalog =
            imageModelOptionCatalog ?? CreateLoadedImageModelOptionCatalog();

        return new UniversalNanoBananaPanelViewModel(
            filePickerService ?? new EmptyFilePickerService(),
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
            new RecordingPromptTextSizeController(),
            new NanoBanana2QuoteViewModel(
                new GenerationPricePreviewEstimator(),
                new NanoBanana2PanelTextFormatter(
                    TestLocalizationTextProvider.Default)),
            new WeakReferenceMessenger(),
            new TestViewModelErrorHandler(),
            TestLocalizationTextProvider.Default,
            new NanoBanana2PanelTextFormatter(
                TestLocalizationTextProvider.Default),
            TestApiConfiguration.CreateStateWritePolicy());
    }

    private static Task AttachImageFromSourceAsync(
        UniversalNanoBananaPanelViewModel viewModel,
        AttachedImageDto image,
        string source)
    {
        return source switch
        {
            PickerAttachmentSource =>
                viewModel.PickImageCommand.ExecuteAsync(null),
            GalleryAttachmentSource =>
                viewModel.AttachImagesCommand.ExecuteAsync([image]),
            DropAttachmentSource =>
                viewModel.AttachImageInputsCommand.ExecuteAsync(
                    [ImageAttachmentInput.FromImage(image)]),
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                "Unsupported attachment input source.")
        };
    }

    private static IImageModelOptionCatalog CreateLoadedImageModelOptionCatalog()
    {
        ImageModelOptionCatalog catalog = new();
        catalog.Initialize(ApiModelMetadataTestCatalog.LoadCatalog());

        return catalog;
    }

    private static void RenderOptionsLayout(ShownPanelContext context)
    {
        Window window = context.Window;
        UpdateOptionsLayout(window);

        if (context.FrameScheduler.HasQueuedFrame)
        {
            context.FrameScheduler.RunNextFrame(TimeSpan.Zero);
            context.FrameScheduler.RunNextFrame(TimeSpan.FromSeconds(1d));
        }

        window.CaptureRenderedFrame();
    }

    private static void ShowGenerationOptions(ShownPanelContext context)
    {
        RenderOptionsLayout(context);

        if (!GetInlineOptionsHost(context.View).IsVisible && !GetTemperaturePopup(context.View).IsOpen)
        {
            OpenTemperaturePopup(context.View);
        }

        UpdateOptionsLayout(context.Window);
    }

    private static Button GetOptionResetButton(NanoBanana2PanelView view, IRelayCommand command)
    {
        StackPanel options = view.FindControl<StackPanel>("GenerationOptionsPanel")
            ?? throw new InvalidOperationException("The generation options panel was not found.");

        return options.GetLogicalDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, command));
    }

    private static void ClickOptionResetButton(Button button)
    {
        TopLevel root = TopLevel.GetTopLevel(button)
            ?? throw new InvalidOperationException("The reset icon was not attached to a window.");
        Point center = GetCenterPoint(button, root);

        root.MouseDown(center, MouseButton.Left);
        root.MouseUp(center, MouseButton.Left);
    }

    private static void UpdateOptionsLayout(Window window)
    {
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
        window.CaptureRenderedFrame();
    }

    private static ContentControl GetInlineOptionsHost(NanoBanana2PanelView view)
    {
        return view.FindControl<ContentControl>("InlineGenerationOptionsHost")
            ?? throw new InvalidOperationException("The inline generation options host was not found.");
    }

    private static ComboBox GetComboBox(NanoBanana2PanelView view, string name)
    {
        return view
            .GetVisualDescendants()
            .OfType<ComboBox>()
            .Single(comboBox => string.Equals(comboBox.Name, name, StringComparison.Ordinal));
    }

    private static TextBox GetPromptTextBox(NanoBanana2PanelView view)
    {
        return view
            .GetVisualDescendants()
            .OfType<TextBox>()
            .Single(textBox => string.Equals(
                textBox.Name,
                "PromptTextBox",
                StringComparison.Ordinal));
    }

    private static Point GetCenterPoint(Control control, Visual target)
    {
        Point? center = control.TranslatePoint(
            new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d), target);

        if (center is null)
        {
            throw new InvalidOperationException("Control position was not found.");
        }

        return center.Value;
    }

    private static double GetRightGap(Control control, Visual target)
    {
        Point? origin = control.TranslatePoint(new Point(0d, 0d), target);

        if (origin is null)
        {
            throw new InvalidOperationException("Control position was not found.");
        }

        return target.Bounds.Width - origin.Value.X - control.Bounds.Width;
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

    private static Popup OpenTemperaturePopup(NanoBanana2PanelView view)
    {
        Popup popup = GetTemperaturePopup(view);
        Button button = GetTemperatureButton(view);

        ToggleTemperaturePopup(button);

        return popup;
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

    private static void AssertTemperaturePanelAnimating(
        Border panel,
        ScaleTransform motionScale,
        TranslateTransform motionTranslate)
    {
        panel.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
        motionScale.ScaleX.Should().BeGreaterThan(0.94d).And.BeLessThan(1d);
        motionScale.ScaleY.Should().BeGreaterThan(0.94d).And.BeLessThan(1d);
        motionTranslate.Y.Should().BeGreaterThan(-10d).And.BeLessThan(0d);
    }

    private static double GetPromptMinimumHeight(NanoBanana2PanelView view)
    {
        return GetDoubleResource(view, PromptHeightResourceKey);
    }

    private static double GetDoubleResource(Control control, string resourceKey)
    {
        if (control.TryFindResource(resourceKey, out object? value)
            && (value is double doubleValue))
        {
            return doubleValue;
        }

        throw new InvalidOperationException($"Double resource '{resourceKey}' was not found.");
    }

    private static Color GetColorResource(Control control, string resourceKey)
    {
        if (control.TryFindResource(resourceKey, control.ActualThemeVariant, out object? value)
            && (value is Color color))
        {
            return color;
        }

        throw new InvalidOperationException($"Color resource '{resourceKey}' was not found.");
    }

    private sealed class ShownPanelContext : IDisposable
    {
        public NanoBanana2PanelView View { get; }
        public UniversalNanoBananaPanelViewModel ViewModel { get; }
        public Window Window { get; }
        public TestUiFrameScheduler FrameScheduler { get; } = new();

        public ShownPanelContext(double? width = null, double? height = null)
        {
            ViewModel = CreateViewModel();
            View = new NanoBanana2PanelView(FrameScheduler)
            {
                DataContext = ViewModel
            };
            Window = width is double panelWidth && height is double panelHeight
                ? Show(View, panelWidth, panelHeight)
                : Show(View);
        }

        public void Dispose()
        {
            Window.Close();
        }
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

    private sealed class FixedFilePickerService : IFilePickerService
    {
        private readonly AttachedImageDto _image;

        public FixedFilePickerService(AttachedImageDto image)
        {
            _image = image;
        }

        public Task<IReadOnlyList<ImageAttachmentInput>> PickImagesAsync(
            int maxInputBytes,
            CancellationToken ct)
        {
            IReadOnlyList<ImageAttachmentInput> inputs =
                [ImageAttachmentInput.FromImage(_image)];

            return Task.FromResult(inputs);
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
