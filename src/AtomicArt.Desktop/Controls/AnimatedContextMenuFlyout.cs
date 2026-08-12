using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls;

public sealed class AnimatedContextMenuFlyout : MenuFlyout
{
    internal const int ClosingAnimationDurationMilliseconds = 60;

    private const string PresenterStyleClass = "animated-context-menu";
    private const string PopupShadowResourceKey = "SukiPopupShadow";

    private AvaloniaUiFrameScheduler? _frameScheduler;
    private UiAnimationScheduler? _animationScheduler;
    private MenuFlyoutPresenter? _presenter;
    private ContextMenuRevealHost? _revealHost;
    private int _animationVersion;
    private bool _isCloseAnimationRunning;
    private bool _isCompletingClose;

    public AnimatedContextMenuFlyout()
    {
        FlyoutPresenterClasses.Add(PresenterStyleClass);
        Popup.InheritsTransform = true;
    }

    protected override void OnOpening(CancelEventArgs args)
    {
        base.OnOpening(args);

        if (args.Cancel)
        {
            return;
        }

        ReleaseAnimationResources();
        _isCloseAnimationRunning = false;
        _isCompletingClose = false;
        _presenter = Popup.Child as MenuFlyoutPresenter;

        if (_presenter is null)
        {
            return;
        }

        Popup.Child = null;
        _revealHost = new ContextMenuRevealHost(_presenter);
        Popup.Child = _revealHost;
        _revealHost.SetBoxShadows(ResolvePopupShadow(_revealHost));
    }

    protected override void OnOpened()
    {
        base.OnOpened();

        if (_revealHost is null)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(_revealHost);
        if (topLevel is null)
        {
            _revealHost.CompleteReveal();
            return;
        }

        _frameScheduler = new AvaloniaUiFrameScheduler(topLevel);
        _animationScheduler = new UiAnimationScheduler(_frameScheduler);
        int animationVersion = ++_animationVersion;
        _animationScheduler.RequestAnimationFrame(
            _ => StartOpeningAnimation(animationVersion, topLevel));
    }

    protected override void OnClosing(CancelEventArgs args)
    {
        if (_isCompletingClose)
        {
            return;
        }

        base.OnClosing(args);

        if (args.Cancel)
        {
            return;
        }

        args.Cancel = true;

        if (_isCloseAnimationRunning)
        {
            return;
        }

        _isCloseAnimationRunning = true;
        int animationVersion = ++_animationVersion;
        ContextMenuRevealHost? revealHost = _revealHost;
        UiAnimationScheduler? animationScheduler = _animationScheduler;
        if (revealHost is null || animationScheduler is null)
        {
            CompleteClose(animationVersion);
            return;
        }

        animationScheduler.Cancel(revealHost);
        _ = animationScheduler.AnimateValueAsync(
            revealHost,
            revealHost.Opacity,
            0d,
            ClosingAnimationDurationMilliseconds,
            0,
            MotionEasing.EaseOutCirc,
            value => revealHost.Opacity = value,
            () => CompleteClose(animationVersion));
    }

    protected override void OnClosed()
    {
        ReleaseAnimationResources();
        RestorePresenterAsPopupChild();
        _revealHost = null;
        _presenter = null;
        _isCloseAnimationRunning = false;
        _isCompletingClose = false;
        base.OnClosed();
    }

    private static BoxShadows ResolvePopupShadow(Control control)
    {
        bool found = control.TryFindResource(
            PopupShadowResourceKey,
            out object? resource);
        if (!found && Application.Current is Application application)
        {
            found = application.TryGetResource(
                PopupShadowResourceKey,
                control.ActualThemeVariant,
                out resource);
        }

        return found && resource is BoxShadows boxShadows
            ? boxShadows
            : default;
    }

    private void StartOpeningAnimation(
        int animationVersion,
        TopLevel topLevel)
    {
        if ((animationVersion != _animationVersion)
            || !IsOpen
            || _isCloseAnimationRunning
            || _presenter is null
            || _revealHost is null
            || _animationScheduler is null)
        {
            return;
        }

        RenderTargetBitmap? snapshot = VisualSnapshotRenderer.Capture(
            topLevel,
            _presenter);
        if (snapshot is null)
        {
            _revealHost.CompleteReveal();
            return;
        }

        ContextMenuRevealOrigin origin = ContextMenuRevealOriginResolver.Resolve(
            _presenter);
        _revealHost.BeginReveal(snapshot, origin);
        _ = _animationScheduler.AnimateValueAsync(
            _revealHost,
            0d,
            1d,
            ContextMenuRevealHost.OpeningDurationMilliseconds,
            0,
            MotionEasing.Linear,
            _revealHost.ApplyOpeningProgress,
            () => CompleteOpeningAnimation(animationVersion));
    }

    private void CompleteOpeningAnimation(int animationVersion)
    {
        if ((animationVersion != _animationVersion)
            || !IsOpen
            || _isCloseAnimationRunning)
        {
            return;
        }

        _revealHost?.CompleteReveal();
    }

    private void CompleteClose(int animationVersion)
    {
        if ((animationVersion != _animationVersion) || !IsOpen)
        {
            return;
        }

        _isCloseAnimationRunning = false;
        _isCompletingClose = true;
        Hide();
        _isCompletingClose = false;
    }

    private void ReleaseAnimationResources()
    {
        _animationVersion++;

        if (_animationScheduler is not null && _revealHost is not null)
        {
            _animationScheduler.Cancel(_revealHost);
        }

        _animationScheduler = null;
        _frameScheduler?.Dispose();
        _frameScheduler = null;
    }

    private void RestorePresenterAsPopupChild()
    {
        if (_revealHost is null)
        {
            return;
        }

        MenuFlyoutPresenter presenter = _revealHost.DetachPresenter();
        _revealHost.Dispose();
        Popup.Child = presenter;
    }
}
