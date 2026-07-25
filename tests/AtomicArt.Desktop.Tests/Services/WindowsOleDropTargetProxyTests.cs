using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Windows;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsOleDropTargetProxyTests
{
    private const int DataFormatUnavailable =
        unchecked((int)0x80040064);
    private const int OleAlreadyInitialized = 1;
    private const int ProxyRegistrationFailed =
        unchecked((int)0x80004005);
    private const int MaximumInputBytes = 1024;
    private const int NativeWindowCoordinate = 0;
    private const int NativeWindowSize = 1;
    private const uint NativeWindowStyle = 0;
    private const uint DragEnterKeyState = 11;
    private const uint DragOverKeyState = 12;
    private const uint DropKeyState = 13;

    private static readonly nint TestWindowHandle = new nint(1);

    [WindowsFact]
    public void ComInterface_WithProxy_CreatesNativeDropTargetInterface()
    {
        WindowsOleDropTargetProxy proxy = CreateProxy();

        nint interfacePointer = Marshal.GetComInterfaceForObject(
            proxy,
            typeof(IOleDropTarget));

        try
        {
            interfacePointer.Should().NotBe(nint.Zero);
        }
        finally
        {
            _ = Marshal.Release(interfacePointer);
        }
    }

    [WindowsFact]
    public void DragLifecycle_WithOrdinaryData_ForwardsAllEventsUnchanged()
    {
        RecordingDropTarget innerTarget = new();
        WindowsOleDropTargetProxy proxy = CreateProxy(innerTarget);
        Mock<IDataObject> dataObjectMock = new();
        dataObjectMock
            .Setup(dataObject => dataObject.QueryGetData(
                ref It.Ref<FORMATETC>.IsAny))
            .Returns(DataFormatUnavailable);
        NativePoint point = new();
        uint dragEnterEffect = 1;
        uint dragOverEffect = 2;
        uint dropEffect = 3;

        int dragEnterResult = proxy.DragEnter(
            dataObjectMock.Object,
            DragEnterKeyState,
            point,
            ref dragEnterEffect);
        int dragOverResult = proxy.DragOver(
            DragOverKeyState,
            point,
            ref dragOverEffect);
        int dragLeaveResult = proxy.DragLeave();
        int dropResult = proxy.Drop(
            dataObjectMock.Object,
            DropKeyState,
            point,
            ref dropEffect);

        dragEnterResult.Should().Be(RecordingDropTarget.DragEnterResult);
        dragEnterEffect.Should().Be(RecordingDropTarget.DragEnterEffect);
        dragOverResult.Should().Be(RecordingDropTarget.DragOverResult);
        dragOverEffect.Should().Be(RecordingDropTarget.DragOverEffect);
        dragLeaveResult.Should().Be(RecordingDropTarget.DragLeaveResult);
        dropResult.Should().Be(RecordingDropTarget.DropResult);
        dropEffect.Should().Be(RecordingDropTarget.DropEffect);
        innerTarget.DragEnterCallCount.Should().Be(1);
        innerTarget.DragOverCallCount.Should().Be(1);
        innerTarget.DragLeaveCallCount.Should().Be(1);
        innerTarget.DropCallCount.Should().Be(1);
        innerTarget.LastDragEnterDataObject.Should()
            .BeSameAs(dataObjectMock.Object);
        innerTarget.LastDropDataObject.Should()
            .BeSameAs(dataObjectMock.Object);
        innerTarget.LastDragEnterKeyState.Should().Be(DragEnterKeyState);
        innerTarget.LastDragOverKeyState.Should().Be(DragOverKeyState);
        innerTarget.LastDropKeyState.Should().Be(DropKeyState);
    }

    [WindowsFact]
    public void Registration_WithNativeWindow_ReplacesAndRestoresDropTarget()
    {
        Action act = VerifyNativeRegistration;

        ExecuteInSta(act);
    }

    [WindowsFact]
    public void Registration_WithoutExistingDropTarget_DisablesVirtualFilesAndLeavesWindowAvailable()
    {
        Action act = VerifyMissingNativeRegistration;

        ExecuteInSta(act);
    }

    [WindowsFact]
    public void Registration_WhenProxyRegistrationFails_RestoresOriginalTarget()
    {
        Action act = VerifyFailedProxyRegistration;

        ExecuteInSta(act);
    }

    private static WindowsOleDropTargetProxy CreateProxy(
        IOleDropTarget? innerTarget = null)
    {
        return new WindowsOleDropTargetProxy(
            innerTarget ?? new RecordingDropTarget(),
            CreateReader(),
            new VirtualFileDropInputSession(),
            () => MaximumInputBytes,
            NullLogger<WindowsOleDropTargetProxy>.Instance);
    }

    private static void VerifyNativeRegistration()
    {
        int initializeResult = OleInitialize(nint.Zero);
        initializeResult.Should().BeOneOf(
            WindowsNativeDragDrop.Succeeded,
            OleAlreadyInitialized);
        nint windowHandle = CreateNativeWindow();
        RecordingDropTarget innerTarget = new();

        try
        {
            int initialRegistration = WindowsNativeDragDrop.RegisterDragDrop(
                windowHandle,
                innerTarget);
            initialRegistration.Should().Be(0);
            nint registeredTargetPointer =
                WindowsNativeDragDrop.GetWindowProperty(
                    windowHandle,
                    WindowsDropTargetRegistration.OleDropTargetWindowProperty);
            registeredTargetPointer.Should().NotBe(
                nint.Zero,
                "the OLE compatibility property must expose Avalonia's "
                + "registered drop target");

            WindowsDropTargetRegistration? registration =
                WindowsDropTargetRegistration.TryCreate(
                    windowHandle,
                    CreateReader(),
                    new VirtualFileDropInputSession(),
                    () => MaximumInputBytes,
                    NullLogger<WindowsOleDropTargetProxy>.Instance,
                    NullLogger<
                        WindowsVirtualFileDropAttachmentService>.Instance);

            registration.Should().NotBeNull();

            if (registration is null)
            {
                throw new InvalidOperationException(
                    "The OLE drop-target proxy should be registered.");
            }

            registration.Dispose(restoreInnerTarget: true);
            nint restoredTargetPointer =
                WindowsNativeDragDrop.GetWindowProperty(
                    windowHandle,
                    WindowsDropTargetRegistration.OleDropTargetWindowProperty);
            restoredTargetPointer.Should().NotBe(
                nint.Zero,
                "disposing the proxy must restore Avalonia's drop target");
            WindowsNativeDragDrop.RevokeDragDrop(windowHandle).Should().Be(0);
        }
        finally
        {
            _ = WindowsNativeDragDrop.RevokeDragDrop(windowHandle);
            _ = DestroyWindow(windowHandle);
            OleUninitialize();
        }
    }

    private static void VerifyMissingNativeRegistration()
    {
        int initializeResult = OleInitialize(nint.Zero);
        initializeResult.Should().BeOneOf(
            WindowsNativeDragDrop.Succeeded,
            OleAlreadyInitialized);
        nint windowHandle = CreateNativeWindow();
        RecordingLogger<WindowsVirtualFileDropAttachmentService> logger =
            new();
        RecordingDropTarget innerTarget = new();

        try
        {
            WindowsDropTargetRegistration? registration =
                WindowsDropTargetRegistration.TryCreate(
                    windowHandle,
                    CreateReader(),
                    new VirtualFileDropInputSession(),
                    () => MaximumInputBytes,
                    NullLogger<WindowsOleDropTargetProxy>.Instance,
                    logger);

            registration.Should().BeNull();
            logger.CallCount.Should().Be(1);
            logger.Level.Should().Be(LogLevel.Warning);
            logger.Message.Should().Contain(
                WindowsDropTargetRegistration.OleDropTargetWindowProperty);
            WindowsNativeDragDrop.RegisterDragDrop(
                windowHandle,
                innerTarget).Should().Be(0);
            WindowsNativeDragDrop.RevokeDragDrop(windowHandle).Should().Be(0);
        }
        finally
        {
            _ = WindowsNativeDragDrop.RevokeDragDrop(windowHandle);
            _ = DestroyWindow(windowHandle);
            OleUninitialize();
        }
    }

    private static void VerifyFailedProxyRegistration()
    {
        int initializeResult = OleInitialize(nint.Zero);
        initializeResult.Should().BeOneOf(
            WindowsNativeDragDrop.Succeeded,
            OleAlreadyInitialized);
        RecordingDropTarget innerTarget = new();
        nint innerTargetPointer = Marshal.GetComInterfaceForObject(
            innerTarget,
            typeof(IOleDropTarget));
        StubWindowsDropTargetNativeApi nativeApi = new(
            innerTargetPointer,
            new int[]
            {
                ProxyRegistrationFailed,
                WindowsNativeDragDrop.Succeeded
            });

        try
        {
            WindowsDropTargetRegistration? registration =
                WindowsDropTargetRegistration.TryCreate(
                    TestWindowHandle,
                    CreateReader(),
                    new VirtualFileDropInputSession(),
                    () => MaximumInputBytes,
                    NullLogger<WindowsOleDropTargetProxy>.Instance,
                    NullLogger<
                        WindowsVirtualFileDropAttachmentService>.Instance,
                    nativeApi);

            registration.Should().BeNull();
            nativeApi.RevokeCallCount.Should().Be(1);
            nativeApi.RegisteredTargets.Should().HaveCount(2);
            nativeApi.RegisteredTargets[0].Should()
                .BeOfType<WindowsOleDropTargetProxy>();
            nativeApi.RegisteredTargets[1].Should()
                .BeSameAs(innerTarget);
        }
        finally
        {
            _ = Marshal.Release(innerTargetPointer);
            OleUninitialize();
        }
    }

    private static WindowsVirtualFileReader CreateReader()
    {
        AttachedImageSignatureValidator signatureValidator = new();

        return new WindowsVirtualFileReader(
            new AttachedImageFileReader(signatureValidator),
            NullLogger<WindowsVirtualFileReader>.Instance);
    }

    private static nint CreateNativeWindow()
    {
        nint windowHandle = CreateWindowEx(
            NativeWindowStyle,
            "STATIC",
            "AtomicArtVirtualFileDropTest",
            NativeWindowStyle,
            NativeWindowCoordinate,
            NativeWindowCoordinate,
            NativeWindowSize,
            NativeWindowSize,
            nint.Zero,
            nint.Zero,
            nint.Zero,
            nint.Zero);
        windowHandle.Should().NotBe(nint.Zero);

        return windowHandle;
    }

    private static void ExecuteInSta(Action action)
    {
        Exception? threadException = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        thread.Join();

        threadException.Should().BeNull();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public int CallCount { get; private set; }
        public LogLevel Level { get; private set; }
        public string Message { get; private set; } = string.Empty;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            _ = state;

            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            _ = logLevel;

            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _ = eventId;
            ArgumentNullException.ThrowIfNull(formatter);

            CallCount++;
            Level = logLevel;
            Message = formatter(state, exception);
        }
    }

    private sealed class RecordingDropTarget : IOleDropTarget
    {
        public const int DragEnterResult = 101;
        public const int DragOverResult = 102;
        public const int DragLeaveResult = 103;
        public const int DropResult = 104;
        public const uint DragEnterEffect = 21;
        public const uint DragOverEffect = 22;
        public const uint DropEffect = 23;

        public int DragEnterCallCount { get; private set; }
        public int DragOverCallCount { get; private set; }
        public int DragLeaveCallCount { get; private set; }
        public int DropCallCount { get; private set; }
        public IDataObject? LastDragEnterDataObject { get; private set; }
        public IDataObject? LastDropDataObject { get; private set; }
        public uint LastDragEnterKeyState { get; private set; }
        public uint LastDragOverKeyState { get; private set; }
        public uint LastDropKeyState { get; private set; }

        public int DragEnter(
            IDataObject dataObject,
            uint keyState,
            NativePoint point,
            ref uint effect)
        {
            _ = point;
            DragEnterCallCount++;
            LastDragEnterDataObject = dataObject;
            LastDragEnterKeyState = keyState;
            effect = DragEnterEffect;

            return DragEnterResult;
        }

        public int DragOver(
            uint keyState,
            NativePoint point,
            ref uint effect)
        {
            _ = point;
            DragOverCallCount++;
            LastDragOverKeyState = keyState;
            effect = DragOverEffect;

            return DragOverResult;
        }

        public int DragLeave()
        {
            DragLeaveCallCount++;

            return DragLeaveResult;
        }

        public int Drop(
            IDataObject dataObject,
            uint keyState,
            NativePoint point,
            ref uint effect)
        {
            _ = point;
            DropCallCount++;
            LastDropDataObject = dataObject;
            LastDropKeyState = keyState;
            effect = DropEffect;

            return DropResult;
        }
    }

    private sealed class StubWindowsDropTargetNativeApi
        : IWindowsDropTargetNativeApi
    {
        public int RevokeCallCount { get; private set; }
        public IReadOnlyList<IOleDropTarget> RegisteredTargets =>
            _registeredTargets;

        private readonly nint _dropTargetPointer;
        private readonly Queue<int> _registerResults;
        private readonly List<IOleDropTarget> _registeredTargets = [];

        public StubWindowsDropTargetNativeApi(
            nint dropTargetPointer,
            IEnumerable<int> registerResults)
        {
            _dropTargetPointer = dropTargetPointer;
            _registerResults = new Queue<int>(registerResults);
        }

        public nint GetWindowProperty(
            nint windowHandle,
            string propertyName)
        {
            _ = windowHandle;
            _ = propertyName;

            return _dropTargetPointer;
        }

        public bool IsWindow(nint windowHandle)
        {
            _ = windowHandle;

            return true;
        }

        public int RegisterDragDrop(
            nint windowHandle,
            IOleDropTarget dropTarget)
        {
            _ = windowHandle;
            _registeredTargets.Add(dropTarget);

            return _registerResults.Dequeue();
        }

        public int RevokeDragDrop(nint windowHandle)
        {
            _ = windowHandle;
            RevokeCallCount++;

            return WindowsNativeDragDrop.Succeeded;
        }
    }

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(nint reserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        EntryPoint = "CreateWindowExW",
        SetLastError = true)]
    private static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint windowHandle);
}
