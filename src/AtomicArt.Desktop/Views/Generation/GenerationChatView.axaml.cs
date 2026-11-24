using System.Collections.Specialized;
using System.ComponentModel;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Views.Generation;

public partial class GenerationChatView : UserControl
{
    public bool IsPresented => _isPresented;
    public GenerationReferencePreviewLoader? PreviewLoader
    {
        get => GetValue(PreviewLoaderProperty);
        internal set => SetValue(PreviewLoaderProperty, value);
    }

    public static readonly DirectProperty<GenerationChatView, bool> IsPresentedProperty =
        AvaloniaProperty.RegisterDirect<GenerationChatView, bool>(
            nameof(IsPresented), view => view.IsPresented);
    public static readonly StyledProperty<GenerationReferencePreviewLoader?> PreviewLoaderProperty =
        AvaloniaProperty.Register<GenerationChatView, GenerationReferencePreviewLoader?>(nameof(PreviewLoader));

    internal ILogger<GenerationChatView> PresentationLogger { get; set; } = NullLogger<GenerationChatView>.Instance;

    private const int BranchFadeMilliseconds = 180;

    private readonly CollectionChangedSubscription _messagesSubscription;
    private GenerationChatViewModel? _chat;
    private AvaloniaUiFrameScheduler? _frames;
    private UiAnimationScheduler? _animation;
    private ScrollViewer? _scrollViewer;
    private bool _isPresented;
    private bool _arrivalQueued;
    private int _arrivalVersion;
    private int _presentationVersion;
    private int _resizeVersion;
    private bool _followMessageResize;

    public GenerationChatView()
    {
        _messagesSubscription = new CollectionChangedSubscription(OnMessagesChanged);
        InitializeComponent();
        ChatItems.TemplateApplied += OnChatItemsTemplateApplied;
        AddHandler(PointerWheelChangedEvent, OnPointerScrolled, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    internal void TrackMessageResize(GenerationChatMessageView message, TimeSpan duration)
    {
        if (_scrollViewer is not { } scroll || !IsPresented
            || (_chat?.Messages.LastOrDefault() != message.DataContext)
            || scroll.ScrollBarMaximum.Y - scroll.Offset.Y > 1d)
        {
            return;
        }

        int version = ++_resizeVersion;
        _followMessageResize = true;
        DispatcherTimer.RunOnce(() =>
        {
            if (version == _resizeVersion)
            {
                _followMessageResize = false;
            }
        }, duration, DispatcherPriority.Loaded);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BindChat();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnbindChat();
        _frames?.Dispose();
        _frames = null;
        _animation = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (this.IsAttachedToVisualTree())
        {
            BindChat();
        }
    }

    private void BindChat()
    {
        UnbindChat();

        if (DataContext is GenerationChatViewModel chat)
        {
            _chat = chat;
            _messagesSubscription.ReplaceSource(chat.Messages);
            chat.PropertyChanging += OnChatChanging;
            chat.PropertyChanged += OnChatChanged;
            UpdatePresentation();
        }
    }

    private void UnbindChat()
    {
        if (_chat is { } chat)
        {
            _messagesSubscription.Clear();
            chat.PropertyChanging -= OnChatChanging;
            chat.PropertyChanged -= OnChatChanged;
            _chat = null;
        }

        UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        int version = ++_presentationVersion;
        CancelArrivalScroll();

        if (_chat?.IsExpanded != true)
        {
            _animation?.Cancel(PreviousBranch);

            if (_scrollViewer is { } scroll)
            {
                _animation?.Cancel(scroll);
                scroll.Opacity = 1d;
                scroll.IsHitTestVisible = true;
            }

            ClearPreviousBranch();
            SetAndRaise(IsPresentedProperty, ref _isPresented, false);

            return;
        }

        Dispatcher.UIThread.Post(() => _ = TryPreparePresentationAsync(version), DispatcherPriority.Loaded);
    }

    private async Task TryPreparePresentationAsync(int version)
    {
        try
        {
            await PreparePresentationAsync(version);
        }
        catch (Exception ex)
        {
            PresentationLogger.LogError(ex, "Could not prepare the chat history for display");

            if (version == _presentationVersion)
            {
                _chat?.CollapseCommand.Execute(null);
            }
        }
    }

    private async Task PreparePresentationAsync(int version)
    {
        if (version != _presentationVersion || _scrollViewer is not { } scroll
            || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        scroll.ScrollToEnd();
        scroll.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Loaded);

        if (version != _presentationVersion)
        {
            return;
        }

        await Task.WhenAll(scroll.GetVisualDescendants()
                    .OfType<GenerationReferencePreviewControl>().Select(preview => preview.PreviewLoad));

        if (version != _presentationVersion)
        {
            return;
        }

        scroll.UpdateLayout();
        scroll.ScrollToEnd();
        topLevel.RequestAnimationFrame(_ => Present(version, topLevel, scroll));
    }

    private void Present(int version, TopLevel topLevel, ScrollViewer scroll)
    {
        if (version != _presentationVersion)
        {
            return;
        }

        SetAndRaise(IsPresentedProperty, ref _isPresented, true);
        scroll.IsHitTestVisible = true;

        if (PreviousBranch.IsVisible || scroll.Opacity < 1d)
        {
            _frames ??= new AvaloniaUiFrameScheduler(topLevel);
            _animation ??= new UiAnimationScheduler(_frames);

            if (PreviousBranch.IsVisible)
            {
                _ = _animation.AnimateValueAsync(PreviousBranch, 1d, 0d, BranchFadeMilliseconds, 0,
                    MotionEasing.EaseOut, opacity => PreviousBranch.Opacity = opacity, ClearPreviousBranch);
            }
            else
            {
                _ = _animation.AnimateValueAsync(scroll, 0d, 1d, BranchFadeMilliseconds, 0,
                    MotionEasing.EaseOut, opacity => scroll.Opacity = opacity);
            }
        }
    }

    private void ClearPreviousBranch()
    {
        PreviousBranch.IsVisible = false;
        RenderTargetBitmap? snapshot = PreviousBranchImage.Source as RenderTargetBitmap;
        PreviousBranchImage.Source = null;
        snapshot?.Dispose();
    }

    private void CancelArrivalScroll()
    {
        _resizeVersion++;
        _followMessageResize = false;
        _arrivalVersion++;
        _arrivalQueued = false;

        if (_scrollViewer is { } scroll)
        {
            SmoothScrollBehavior.CancelScrollToEnd(scroll);
        }
    }

    private void OnChatChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName != nameof(GenerationChatViewModel.ConversationVersion)
            || !IsPresented || _scrollViewer is not { } scroll || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        _animation?.Cancel(PreviousBranch);
        scroll.IsHitTestVisible = false;

        if (PreviousBranchImage.Source is not null)
        {
            PreviousBranch.Opacity = 1d;

            return;
        }

        try
        {
            PreviousBranchImage.Source = VisualSnapshotRenderer.Capture(topLevel, scroll);
        }
        catch (Exception ex)
        {
            PresentationLogger.LogWarning(ex, "Could not capture the previous chat history");
        }

        PreviousBranch.IsVisible = PreviousBranchImage.Source is not null;
        PreviousBranch.Opacity = 1d;
        scroll.Opacity = PreviousBranch.IsVisible ? 1d : 0d;
    }

    private void OnChatChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationChatViewModel.Messages))
        {
            _messagesSubscription.ReplaceSource(_chat?.Messages);
        }

        if (e.PropertyName is nameof(GenerationChatViewModel.IsExpanded)
            or nameof(GenerationChatViewModel.ConversationVersion))
        {
            UpdatePresentation();
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsPresented || _arrivalQueued || _scrollViewer is not { } scroll
            || e.NewItems is null || !e.NewItems.OfType<GenerationChatMessageViewModel>().Any(message => message.AnimateInsertion))
        {
            return;
        }

        _arrivalQueued = true;
        int arrival = ++_arrivalVersion;
        int version = _presentationVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (arrival != _arrivalVersion)
            {
                return;
            }

            _arrivalQueued = false;

            if (IsPresented && version == _presentationVersion
                && this.FindResource(GenerationChatMessageView.MessageArrivalDurationResourceKey) is TimeSpan duration)
            {
                SmoothScrollBehavior.ScrollToEnd(scroll, duration);
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnChatItemsTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (_scrollViewer is { } previous)
        {
            _animation?.Cancel(previous);
            previous.ScrollChanged -= OnChatScrolled;
        }

        _scrollViewer = e.NameScope.Find<ScrollViewer>("ChatScrollViewer");

        if (_scrollViewer is { } scroll)
        {
            scroll.ScrollChanged += OnChatScrolled;
        }

        UpdatePresentation();
    }

    private void OnChatScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (_followMessageResize && e.ExtentDelta.Y != 0d && sender is ScrollViewer scroll)
        {
            scroll.ScrollToEnd();
        }
    }

    private void OnPointerScrolled(object? sender, PointerWheelEventArgs e)
    {
        CancelArrivalScroll();
    }
}
