using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.Windows;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[SupportedOSPlatform("windows")]
internal sealed class WindowsOleDropTargetProxy : IOleDropTarget
{
    private readonly IOleDropTarget _innerTarget;
    private readonly WindowsVirtualFileReader _virtualFileReader;
    private readonly VirtualFileDropInputSession _inputSession;
    private readonly Func<int> _getMaximumInputBytes;
    private readonly ILogger<WindowsOleDropTargetProxy> _logger;

    public WindowsOleDropTargetProxy(
        IOleDropTarget innerTarget,
        WindowsVirtualFileReader virtualFileReader,
        VirtualFileDropInputSession inputSession,
        Func<int> getMaximumInputBytes,
        ILogger<WindowsOleDropTargetProxy> logger)
    {
        ArgumentNullException.ThrowIfNull(innerTarget);
        ArgumentNullException.ThrowIfNull(virtualFileReader);
        ArgumentNullException.ThrowIfNull(inputSession);
        ArgumentNullException.ThrowIfNull(getMaximumInputBytes);
        ArgumentNullException.ThrowIfNull(logger);

        _innerTarget = innerTarget;
        _virtualFileReader = virtualFileReader;
        _inputSession = inputSession;
        _getMaximumInputBytes = getMaximumInputBytes;
        _logger = logger;
    }

    public int DragEnter(
        IDataObject dataObject,
        uint keyState,
        NativePoint point,
        ref uint effect)
    {
        return _innerTarget.DragEnter(
            dataObject,
            keyState,
            point,
            ref effect);
    }

    public int DragOver(
        uint keyState,
        NativePoint point,
        ref uint effect)
    {
        return _innerTarget.DragOver(keyState, point, ref effect);
    }

    public int DragLeave()
    {
        return _innerTarget.DragLeave();
    }

    public int Drop(
        IDataObject dataObject,
        uint keyState,
        NativePoint point,
        ref uint effect)
    {
        IReadOnlyList<ImageAttachmentInput> inputs;

        try
        {
            int maxInputBytes = _getMaximumInputBytes();
            inputs = maxInputBytes > 0
                ? _virtualFileReader.ReadInputs(dataObject, maxInputBytes)
                : Array.Empty<ImageAttachmentInput>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Virtual file drop interception failed with error type {ErrorType} and HRESULT {HResult}.",
                ex.GetType().Name,
                ex.HResult);
            inputs = Array.Empty<ImageAttachmentInput>();
        }

        if (inputs.Count == 0)
        {
            return _innerTarget.Drop(
                dataObject,
                keyState,
                point,
                ref effect);
        }

        IDisposable scope;

        try
        {
            scope = _inputSession.Begin(inputs);
        }
        catch (InvalidOperationException ex)
        {
            foreach (ImageAttachmentInput input in inputs)
            {
                input.Dispose();
            }

            _logger.LogWarning(
                "Virtual file drop session could not be started. HRESULT {HResult}.",
                ex.HResult);

            return _innerTarget.Drop(
                dataObject,
                keyState,
                point,
                ref effect);
        }

        using (scope)
        {
            return _innerTarget.Drop(
                dataObject,
                keyState,
                point,
                ref effect);
        }
    }
}
