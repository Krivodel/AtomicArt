using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.Windows;

internal sealed class WindowsDropTargetRegistration
{
    // OLE has no public getter for a window's registered IDropTarget.
    // This window property lets the proxy preserve Avalonia's target instead
    // of reimplementing its drag-and-drop routing.
    internal const string OleDropTargetWindowProperty =
        "OleDropTargetInterface";

    private readonly nint _windowHandle;
    private readonly IOleDropTarget _innerTarget;
    private readonly IWindowsDropTargetNativeApi _nativeApi;
    private WindowsOleDropTargetProxy? _proxy;

    private WindowsDropTargetRegistration(
        nint windowHandle,
        IOleDropTarget innerTarget,
        WindowsOleDropTargetProxy proxy,
        IWindowsDropTargetNativeApi nativeApi)
    {
        _windowHandle = windowHandle;
        _innerTarget = innerTarget;
        _proxy = proxy;
        _nativeApi = nativeApi;
    }

    [SupportedOSPlatform("windows")]
    public static WindowsDropTargetRegistration? TryCreate(
        nint windowHandle,
        WindowsVirtualFileReader virtualFileReader,
        VirtualFileDropInputSession inputSession,
        Func<int> getMaximumInputBytes,
        ILogger<WindowsOleDropTargetProxy> proxyLogger,
        ILogger<WindowsVirtualFileDropAttachmentService> attachmentLogger)
    {
        return TryCreate(
            windowHandle,
            virtualFileReader,
            inputSession,
            getMaximumInputBytes,
            proxyLogger,
            attachmentLogger,
            WindowsDropTargetNativeApi.Instance);
    }

    [SupportedOSPlatform("windows")]
    public void Dispose(bool restoreInnerTarget)
    {
        WindowsOleDropTargetProxy? proxy = Interlocked.Exchange(
            ref _proxy,
            null);

        if (proxy is null)
        {
            return;
        }

        if (_nativeApi.IsWindow(_windowHandle))
        {
            _ = _nativeApi.RevokeDragDrop(_windowHandle);

            if (restoreInnerTarget)
            {
                _ = _nativeApi.RegisterDragDrop(
                    _windowHandle,
                    _innerTarget);
            }
        }

        ReleaseComObject(_innerTarget);
    }

    [SupportedOSPlatform("windows")]
    internal static WindowsDropTargetRegistration? TryCreate(
        nint windowHandle,
        WindowsVirtualFileReader virtualFileReader,
        VirtualFileDropInputSession inputSession,
        Func<int> getMaximumInputBytes,
        ILogger<WindowsOleDropTargetProxy> proxyLogger,
        ILogger<WindowsVirtualFileDropAttachmentService> attachmentLogger,
        IWindowsDropTargetNativeApi nativeApi)
    {
        ArgumentNullException.ThrowIfNull(nativeApi);

        nint innerTargetPointer = nativeApi.GetWindowProperty(
            windowHandle,
            OleDropTargetWindowProperty);

        if (innerTargetPointer == nint.Zero)
        {
            attachmentLogger.LogWarning(
                "The OLE drop-target compatibility property {PropertyName} was not available. "
                + "Windows virtual file drop support was disabled.",
                OleDropTargetWindowProperty);
            return null;
        }

        object innerTargetObject = Marshal.GetObjectForIUnknown(
            innerTargetPointer);

        if (innerTargetObject is not IOleDropTarget innerTarget)
        {
            ReleaseComObject(innerTargetObject);
            attachmentLogger.LogWarning(
                "Avalonia's Windows drop target did not expose IDropTarget.");
            return null;
        }

        WindowsOleDropTargetProxy proxy = new(
            innerTarget,
            virtualFileReader,
            inputSession,
            getMaximumInputBytes,
            proxyLogger);
        int revokeResult = nativeApi.RevokeDragDrop(windowHandle);

        if (revokeResult != WindowsNativeDragDrop.Succeeded)
        {
            ReleaseComObject(innerTargetObject);
            attachmentLogger.LogWarning(
                "Avalonia's Windows drop target could not be temporarily replaced. HRESULT {HResult}.",
                revokeResult);
            return null;
        }

        int registerResult = nativeApi.RegisterDragDrop(
            windowHandle,
            proxy);

        if (registerResult == WindowsNativeDragDrop.Succeeded)
        {
            return new WindowsDropTargetRegistration(
                windowHandle,
                innerTarget,
                proxy,
                nativeApi);
        }

        int restoreResult = nativeApi.RegisterDragDrop(
            windowHandle,
            innerTarget);
        ReleaseComObject(innerTargetObject);
        attachmentLogger.LogWarning(
            "The Windows virtual file drop proxy could not be registered. HRESULT {HResult}.",
            registerResult);

        if (restoreResult != WindowsNativeDragDrop.Succeeded)
        {
            attachmentLogger.LogError(
                "Avalonia's Windows drop target could not be restored. HRESULT {HResult}.",
                restoreResult);
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static void ReleaseComObject(object value)
    {
        if (Marshal.IsComObject(value))
        {
            _ = Marshal.ReleaseComObject(value);
        }
    }
}
