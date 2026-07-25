using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace AtomicArt.Desktop.Services.Windows;

[ComImport]
[Guid("00000122-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleDropTarget
{
    [PreserveSig]
    int DragEnter(
        [MarshalAs(UnmanagedType.Interface)] IDataObject dataObject,
        uint keyState,
        NativePoint point,
        ref uint effect);

    [PreserveSig]
    int DragOver(
        uint keyState,
        NativePoint point,
        ref uint effect);

    [PreserveSig]
    int DragLeave();

    [PreserveSig]
    int Drop(
        [MarshalAs(UnmanagedType.Interface)] IDataObject dataObject,
        uint keyState,
        NativePoint point,
        ref uint effect);
}
