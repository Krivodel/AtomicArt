using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GenerationMetadataOverlayView : UserControl
{
    private const double PreviewEntryOffsetX = -28d;
    private const double SummaryEntryOffsetX = 24d;
    private const double ContentEntryOffsetY = 24d;

    private static readonly TimeSpan PanelOpeningDuration = TimeSpan.FromMilliseconds(90d);
    private static readonly TimeSpan EntryOpeningDuration = TimeSpan.FromMilliseconds(120d);
    private static readonly TimeSpan PreviewOpeningDelay = TimeSpan.FromMilliseconds(17.5d);
    private static readonly TimeSpan DateOpeningDelay = TimeSpan.FromMilliseconds(35d);
    private static readonly TimeSpan DurationOpeningDelay = TimeSpan.FromMilliseconds(52.5d);
    private static readonly TimeSpan StatusOpeningDelay = TimeSpan.FromMilliseconds(70d);
    private static readonly TimeSpan ParametersOpeningDelay = TimeSpan.FromMilliseconds(82.5d);
    private static readonly TimeSpan PriceOpeningDelay = TimeSpan.FromMilliseconds(87.5d);
    private static readonly TimeSpan PromptOpeningDelay = TimeSpan.FromMilliseconds(100d);
    private static readonly TimeSpan PathOpeningDelay = TimeSpan.FromMilliseconds(117.5d);
    private static readonly TimeSpan RepeatOpeningDelay = TimeSpan.FromMilliseconds(135d);
    private static readonly TimeSpan PanelClosingDuration = TimeSpan.FromMilliseconds(53.333d);
    private static readonly TimeSpan EntryClosingDuration = TimeSpan.FromMilliseconds(60d);
    private static readonly TimeSpan RepeatClosingDelay = TimeSpan.Zero;
    private static readonly TimeSpan PathClosingDelay = TimeSpan.FromMilliseconds(5d);
    private static readonly TimeSpan PromptClosingDelay = TimeSpan.FromMilliseconds(10d);
    private static readonly TimeSpan ParametersClosingDelay = TimeSpan.FromMilliseconds(15d);
    private static readonly TimeSpan PriceClosingDelay = TimeSpan.Zero;
    private static readonly TimeSpan StatusClosingDelay = TimeSpan.FromMilliseconds(5d);
    private static readonly TimeSpan DurationClosingDelay = TimeSpan.FromMilliseconds(10d);
    private static readonly TimeSpan DateClosingDelay = TimeSpan.FromMilliseconds(15d);
    private static readonly TimeSpan PreviewClosingDelay = TimeSpan.FromMilliseconds(10d);
    private static readonly TimeSpan PanelClosingDelay = TimeSpan.FromMilliseconds(30d);
    private static readonly TimeSpan ClosingActionDelay = TimeSpan.FromMilliseconds(90d);

    private GenerationMetadataViewModel? _subscribedViewModel;
    private bool _isAttachedToVisualTree;
    private bool _hasStartedOpeningAnimation;
    private bool _isClosing;

    public GenerationMetadataOverlayView()
    {
        InitializeComponent();
        PrepareOpeningAnimation();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private static void ConfigureOpacityTransition(
        Control control,
        TimeSpan duration,
        TimeSpan delay,
        Easing easing)
    {
        control.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = duration,
                Delay = delay,
                Easing = easing
            }
        ];
    }

    private static void ConfigureHorizontalTransition(
        Control control,
        TimeSpan duration,
        TimeSpan delay,
        Easing easing)
    {
        TranslateTransform translation =
            control.RenderTransform as TranslateTransform ?? new TranslateTransform();
        translation.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = duration,
                Delay = delay,
                Easing = easing
            }
        ];
        control.RenderTransform = translation;
        ConfigureOpacityTransition(control, duration, delay, easing);
    }

    private static void ConfigureHorizontalTranslationTransition(
        Control control,
        TimeSpan duration,
        TimeSpan delay,
        Easing easing)
    {
        TranslateTransform translation =
            control.RenderTransform as TranslateTransform ?? new TranslateTransform();
        translation.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = duration,
                Delay = delay,
                Easing = easing
            }
        ];
        control.RenderTransform = translation;
    }

    private static void ConfigureVerticalTransition(
        Control control,
        TimeSpan duration,
        TimeSpan delay,
        Easing easing)
    {
        TranslateTransform translation =
            control.RenderTransform as TranslateTransform ?? new TranslateTransform();
        translation.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = duration,
                Delay = delay,
                Easing = easing
            }
        ];
        control.RenderTransform = translation;
        ConfigureOpacityTransition(control, duration, delay, easing);
    }

    private static void PrepareOpeningPanelAnimation(Control control)
    {
        ConfigureOpacityTransition(
            control,
            PanelOpeningDuration,
            TimeSpan.Zero,
            new CubicEaseOut());
        control.Opacity = 0d;
    }

    private static void PrepareOpeningHorizontalAnimation(
        Control control,
        double offset,
        TimeSpan delay)
    {
        ConfigureHorizontalTransition(
            control,
            EntryOpeningDuration,
            delay,
            new CubicEaseOut());

        if (control.RenderTransform is TranslateTransform translation)
        {
            translation.X = offset;
        }

        control.Opacity = 0d;
    }

    private static void PrepareOpeningPreviewAnimation(
        Control control,
        double offset,
        TimeSpan delay)
    {
        ConfigureHorizontalTranslationTransition(
            control,
            EntryOpeningDuration,
            delay,
            new CubicEaseOut());

        if (control.RenderTransform is TranslateTransform translation)
        {
            translation.X = offset;
        }
    }

    private static void PrepareOpeningVerticalAnimation(
        Control control,
        double offset,
        TimeSpan delay)
    {
        ConfigureVerticalTransition(
            control,
            EntryOpeningDuration,
            delay,
            new CubicEaseOut());

        if (control.RenderTransform is TranslateTransform translation)
        {
            translation.Y = offset;
        }

        control.Opacity = 0d;
    }

    private static void StartClosingPanelAnimation(Control control)
    {
        ConfigureOpacityTransition(
            control,
            PanelClosingDuration,
            PanelClosingDelay,
            new CubicEaseIn());
        control.Opacity = 0d;
    }

    private static void StartClosingHorizontalAnimation(
        Control control,
        double offset,
        TimeSpan delay)
    {
        ConfigureHorizontalTransition(
            control,
            EntryClosingDuration,
            delay,
            new CubicEaseIn());

        if (control.RenderTransform is TranslateTransform translation)
        {
            translation.X = offset;
        }

        control.Opacity = 0d;
    }

    private static void StartClosingPreviewAnimation(
        Control control,
        double offset,
        TimeSpan delay)
    {
        ConfigureHorizontalTranslationTransition(
            control,
            EntryClosingDuration,
            delay,
            new CubicEaseIn());

        if (control.RenderTransform is TranslateTransform translation)
        {
            translation.X = offset;
        }
    }

    private static void StartClosingVerticalAnimation(
        Control control,
        double offset,
        TimeSpan delay)
    {
        ConfigureVerticalTransition(
            control,
            EntryClosingDuration,
            delay,
            new CubicEaseIn());

        if (control.RenderTransform is TranslateTransform translation)
        {
            translation.Y = offset;
        }

        control.Opacity = 0d;
    }

    private static void ShowAnimatedControl(Control control)
    {
        control.Opacity = 1d;

        if (control.RenderTransform is TranslateTransform translation)
        {
            translation.X = 0d;
            translation.Y = 0d;
        }
    }

    private static void ShowTranslatedControl(Control control)
    {
        if (control.RenderTransform is TranslateTransform translation)
        {
            translation.X = 0d;
        }
    }

    private static void ShowPanel(Control control)
    {
        control.Opacity = 1d;
    }

    private void PrepareOpeningAnimation()
    {
        PrepareOpeningPanelAnimation(PanelRoot);
        PrepareOpeningPreviewAnimation(
            PreviewEntry,
            PreviewEntryOffsetX,
            PreviewOpeningDelay);
        PrepareOpeningHorizontalAnimation(
            DateEntry,
            SummaryEntryOffsetX,
            DateOpeningDelay);
        PrepareOpeningHorizontalAnimation(
            DurationEntry,
            SummaryEntryOffsetX,
            DurationOpeningDelay);
        PrepareOpeningHorizontalAnimation(
            StatusEntry,
            SummaryEntryOffsetX,
            StatusOpeningDelay);
        PrepareOpeningHorizontalAnimation(
            PriceEntry,
            SummaryEntryOffsetX,
            PriceOpeningDelay);
        PrepareOpeningVerticalAnimation(
            ParametersEntry,
            ContentEntryOffsetY,
            ParametersOpeningDelay);
        PrepareOpeningVerticalAnimation(
            PromptEntry,
            ContentEntryOffsetY,
            PromptOpeningDelay);
        PrepareOpeningVerticalAnimation(
            PathEntry,
            ContentEntryOffsetY,
            PathOpeningDelay);
        PrepareOpeningVerticalAnimation(
            RepeatEntry,
            ContentEntryOffsetY,
            RepeatOpeningDelay);
    }

    private void StartOpeningAnimation()
    {
        if (_hasStartedOpeningAnimation)
        {
            return;
        }

        _hasStartedOpeningAnimation = true;
        ShowPanel(PanelRoot);
        ShowTranslatedControl(PreviewEntry);
        ShowAnimatedControl(DateEntry);
        ShowAnimatedControl(DurationEntry);
        ShowAnimatedControl(StatusEntry);
        ShowAnimatedControl(PriceEntry);
        ShowAnimatedControl(ParametersEntry);
        ShowAnimatedControl(PromptEntry);
        ShowAnimatedControl(PathEntry);
        ShowAnimatedControl(RepeatEntry);
    }

    private void StartClosingAnimation()
    {
        StartClosingPreviewAnimation(
            PreviewEntry,
            PreviewEntryOffsetX,
            PreviewClosingDelay);
        StartClosingHorizontalAnimation(
            DateEntry,
            SummaryEntryOffsetX,
            DateClosingDelay);
        StartClosingHorizontalAnimation(
            DurationEntry,
            SummaryEntryOffsetX,
            DurationClosingDelay);
        StartClosingHorizontalAnimation(
            StatusEntry,
            SummaryEntryOffsetX,
            StatusClosingDelay);
        StartClosingHorizontalAnimation(
            PriceEntry,
            SummaryEntryOffsetX,
            PriceClosingDelay);
        StartClosingVerticalAnimation(
            ParametersEntry,
            ContentEntryOffsetY,
            ParametersClosingDelay);
        StartClosingVerticalAnimation(
            PromptEntry,
            ContentEntryOffsetY,
            PromptClosingDelay);
        StartClosingVerticalAnimation(
            PathEntry,
            ContentEntryOffsetY,
            PathClosingDelay);
        StartClosingVerticalAnimation(
            RepeatEntry,
            ContentEntryOffsetY,
            RepeatClosingDelay);
        StartClosingPanelAnimation(PanelRoot);
    }

    private void SubscribeToViewModel(GenerationMetadataViewModel? viewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, viewModel))
        {
            return;
        }

        UnsubscribeFromViewModel();
        _subscribedViewModel = viewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ActionRequested += OnActionRequested;
        }
    }

    private void UnsubscribeFromViewModel()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        _subscribedViewModel.ActionRequested -= OnActionRequested;
        _subscribedViewModel = null;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;

        _isAttachedToVisualTree = true;
        SubscribeToViewModel(DataContext as GenerationMetadataViewModel);
        Dispatcher.Post(StartOpeningAnimation, DispatcherPriority.Loaded);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;

        _isAttachedToVisualTree = false;
        UnsubscribeFromViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_isAttachedToVisualTree)
        {
            return;
        }

        SubscribeToViewModel(DataContext as GenerationMetadataViewModel);
    }

    private async void OnActionRequested(
        object? sender,
        GenerationMetadataActionRequestedEventArgs e)
    {
        _ = sender;

        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        StartClosingAnimation();
        await Task.Delay(ClosingActionDelay);

        if (e.Command.CanExecute(e.Parameter))
        {
            e.Command.Execute(e.Parameter);
        }
    }
}
