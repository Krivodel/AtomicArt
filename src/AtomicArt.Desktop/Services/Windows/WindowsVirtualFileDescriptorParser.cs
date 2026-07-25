using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace AtomicArt.Desktop.Services.Windows;

internal static class WindowsVirtualFileDescriptorParser
{
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint HasFileAttributes = 0x00000004;
    private const uint HasFileSize = 0x00000040;
    private const int MaximumVirtualFileCount = 64;

    public static IReadOnlyList<WindowsVirtualFileDescriptor> Parse(
        ReadOnlySpan<byte> data,
        bool isUnicode)
    {
        if (data.Length < sizeof(uint))
        {
            throw new InvalidDataException(
                "The virtual file descriptor is incomplete.");
        }

        uint itemCount = BinaryPrimitives.ReadUInt32LittleEndian(data);

        if (itemCount > MaximumVirtualFileCount)
        {
            throw new InvalidDataException(
                "The virtual file descriptor contains too many files.");
        }

        int descriptorSize = isUnicode
            ? Marshal.SizeOf<NativeFileDescriptorUnicode>()
            : Marshal.SizeOf<NativeFileDescriptorAnsi>();
        long requiredBytes = sizeof(uint) + ((long)itemCount * descriptorSize);

        if (requiredBytes > data.Length)
        {
            throw new InvalidDataException(
                "The virtual file descriptor is incomplete.");
        }

        List<WindowsVirtualFileDescriptor> descriptors =
            new(checked((int)itemCount));

        for (int index = 0; index < itemCount; index++)
        {
            int offset = checked(sizeof(uint) + (index * descriptorSize));
            WindowsVirtualFileDescriptor descriptor = isUnicode
                ? CreateDescriptor(
                    ReadDescriptor<NativeFileDescriptorUnicode>(
                        data.Slice(offset, descriptorSize)))
                : CreateDescriptor(
                    ReadDescriptor<NativeFileDescriptorAnsi>(
                        data.Slice(offset, descriptorSize)));
            descriptors.Add(descriptor);
        }

        return descriptors;
    }

    private static T ReadDescriptor<T>(ReadOnlySpan<byte> data)
        where T : struct
    {
        nint buffer = Marshal.AllocHGlobal(data.Length);

        try
        {
            Marshal.Copy(data.ToArray(), 0, buffer, data.Length);

            return Marshal.PtrToStructure<T>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static WindowsVirtualFileDescriptor CreateDescriptor(
        NativeFileDescriptorUnicode descriptor)
    {
        return CreateDescriptor(
            descriptor.Flags,
            descriptor.FileAttributes,
            descriptor.FileSizeHigh,
            descriptor.FileSizeLow,
            descriptor.FileName);
    }

    private static WindowsVirtualFileDescriptor CreateDescriptor(
        NativeFileDescriptorAnsi descriptor)
    {
        return CreateDescriptor(
            descriptor.Flags,
            descriptor.FileAttributes,
            descriptor.FileSizeHigh,
            descriptor.FileSizeLow,
            descriptor.FileName);
    }

    private static WindowsVirtualFileDescriptor CreateDescriptor(
        uint flags,
        uint fileAttributes,
        uint fileSizeHigh,
        uint fileSizeLow,
        string fileName)
    {
        ulong? declaredSize = (flags & HasFileSize) != 0
            ? ((ulong)fileSizeHigh << 32) | fileSizeLow
            : null;
        bool isDirectory = (flags & HasFileAttributes) != 0
            && (fileAttributes & FileAttributeDirectory) != 0;

        return new WindowsVirtualFileDescriptor(
            fileName ?? string.Empty,
            declaredSize,
            isDirectory);
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode,
        Pack = 4)]
    private struct NativeFileDescriptorUnicode
    {
        public uint Flags;
        public Guid ClassId;
        public NativeSize Size;
        public NativePoint Position;
        public uint FileAttributes;
        public NativeFileTime CreationTime;
        public NativeFileTime LastAccessTime;
        public NativeFileTime LastWriteTime;
        public uint FileSizeHigh;
        public uint FileSizeLow;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string FileName;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Ansi,
        Pack = 4)]
    private struct NativeFileDescriptorAnsi
    {
        public uint Flags;
        public Guid ClassId;
        public NativeSize Size;
        public NativePoint Position;
        public uint FileAttributes;
        public NativeFileTime CreationTime;
        public NativeFileTime LastAccessTime;
        public NativeFileTime LastWriteTime;
        public uint FileSizeHigh;
        public uint FileSizeLow;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string FileName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize
    {
        public readonly int Width;
        public readonly int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeFileTime
    {
        public readonly uint LowDateTime;
        public readonly uint HighDateTime;
    }
}
