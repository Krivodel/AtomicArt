using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Windows;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsVirtualFileReaderTests
{
    private const int MaxInputBytes = 1024;

    [WindowsFact]
    public async Task ReadInputs_WithUnicodeVirtualFile_ReturnsBufferedImage()
    {
        byte[] content = GenerationImageFileSignatures.Png.ToArray();
        byte[] descriptors = WindowsVirtualFileTestData.CreateDescriptorGroup(
            WindowsVirtualFileTestData.UnicodeDescriptorSize,
            @"folder\virtual.png",
            Encoding.Unicode,
            (ulong)content.Length);
        VirtualFileDataObject dataObject = new(descriptors, content);
        WindowsVirtualFileReader reader = CreateReader();

        IReadOnlyList<ImageAttachmentInput> inputs = reader.ReadInputs(
            dataObject,
            MaxInputBytes);
        AttachedImageDto? image = await inputs.Single().ReadAsync(
            CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException(
                "The buffered virtual image should be available.");
        actualImage.FileName.Should().Be("virtual.png");
        actualImage.ContentType.Should().Be(GenerationImageContentTypes.Png);
        actualImage.Content.Should().Equal(content);
        dataObject.ContentReadCount.Should().Be(1);
    }

    [WindowsFact]
    public async Task ReadInputs_WithStreamContents_ReturnsBufferedImage()
    {
        byte[] content = GenerationImageFileSignatures.Png.ToArray();
        byte[] descriptors = WindowsVirtualFileTestData.CreateDescriptorGroup(
            WindowsVirtualFileTestData.UnicodeDescriptorSize,
            "virtual.png",
            Encoding.Unicode,
            (ulong)content.Length);
        VirtualFileDataObject dataObject = new(
            descriptors,
            content,
            useStreamContents: true);
        WindowsVirtualFileReader reader = CreateReader();

        IReadOnlyList<ImageAttachmentInput> inputs = reader.ReadInputs(
            dataObject,
            MaxInputBytes);
        AttachedImageDto? image = await inputs.Single().ReadAsync(
            CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException(
                "The streamed virtual image should be available.");
        actualImage.Content.Should().Equal(content);
    }

    [WindowsFact]
    public async Task ReadInputs_WithOversizedDeclaredFile_DefersSizeError()
    {
        byte[] descriptors = WindowsVirtualFileTestData.CreateDescriptorGroup(
            WindowsVirtualFileTestData.UnicodeDescriptorSize,
            "virtual.png",
            Encoding.Unicode,
            MaxInputBytes + 1u);
        VirtualFileDataObject dataObject = new(
            descriptors,
            new byte[] { 1 });
        WindowsVirtualFileReader reader = CreateReader();
        IReadOnlyList<ImageAttachmentInput> inputs = reader.ReadInputs(
            dataObject,
            MaxInputBytes);

        Func<Task> act = () => inputs.Single().ReadAsync(
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        dataObject.ContentReadCount.Should().Be(0);
    }

    private static WindowsVirtualFileReader CreateReader()
    {
        AttachedImageSignatureValidator signatureValidator = new();

        return new WindowsVirtualFileReader(
            new AttachedImageFileReader(signatureValidator),
            NullLogger<WindowsVirtualFileReader>.Instance,
            TestApiConfiguration.CreateDataTransferOptionsWrapper());
    }

    private sealed class VirtualFileDataObject(
        byte[] descriptors,
        byte[] content,
        bool useStreamContents = false) : IDataObject
    {
        private readonly short _descriptorFormat = RegisterFormat(
            "FileGroupDescriptorW");
        private readonly short _contentsFormat = RegisterFormat("FileContents");

        public int ContentReadCount { get; private set; }

        public void GetData(ref FORMATETC format, out STGMEDIUM medium)
        {
            if (format.cfFormat == _descriptorFormat)
            {
                medium = CreateMedium(descriptors);
                return;
            }

            if (format.cfFormat == _contentsFormat && format.lindex == 0)
            {
                ContentReadCount++;
                medium = useStreamContents
                    ? CreateStreamMedium(content)
                    : CreateMedium(content);
                return;
            }

            throw new COMException("Format is unavailable.", -2147221404);
        }

        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium)
        {
            throw new NotSupportedException();
        }

        public int QueryGetData(ref FORMATETC format)
        {
            return format.cfFormat == _descriptorFormat
                ? 0
                : -2147221404;
        }

        public int GetCanonicalFormatEtc(
            ref FORMATETC formatIn,
            out FORMATETC formatOut)
        {
            formatOut = default;
            return -2147467263;
        }

        public void SetData(
            ref FORMATETC formatIn,
            ref STGMEDIUM medium,
            bool release)
        {
            throw new NotSupportedException();
        }

        public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
        {
            throw new NotSupportedException();
        }

        public int DAdvise(
            ref FORMATETC format,
            ADVF advf,
            IAdviseSink adviseSink,
            out int connection)
        {
            connection = 0;
            return -2147221501;
        }

        public void DUnadvise(int connection)
        {
            throw new NotSupportedException();
        }

        public int EnumDAdvise(out IEnumSTATDATA? enumAdvise)
        {
            enumAdvise = null;
            return -2147221501;
        }

        private static STGMEDIUM CreateMedium(byte[] data)
        {
            nint memoryHandle = GlobalAlloc(0x0002, (nuint)data.Length);

            if (memoryHandle == nint.Zero)
            {
                throw new InvalidOperationException(
                    "Global memory allocation failed.");
            }

            nint dataPointer = GlobalLock(memoryHandle);

            if (dataPointer == nint.Zero)
            {
                _ = GlobalFree(memoryHandle);
                throw new InvalidOperationException(
                    "Global memory locking failed.");
            }

            try
            {
                Marshal.Copy(data, 0, dataPointer, data.Length);
            }
            finally
            {
                _ = GlobalUnlock(memoryHandle);
            }

            return new STGMEDIUM
            {
                tymed = TYMED.TYMED_HGLOBAL,
                unionmember = memoryHandle
            };
        }

        private static STGMEDIUM CreateStreamMedium(byte[] data)
        {
            int result = CreateStreamOnHGlobal(
                nint.Zero,
                deleteOnRelease: true,
                out IStream stream);

            if (result != 0)
            {
                throw new COMException(
                    "COM stream creation failed.",
                    result);
            }

            stream.Write(data, data.Length, nint.Zero);
            stream.Seek(0, 0, nint.Zero);
            nint streamPointer = Marshal.GetComInterfaceForObject(
                stream,
                typeof(IStream));
            _ = Marshal.ReleaseComObject(stream);

            return new STGMEDIUM
            {
                tymed = TYMED.TYMED_ISTREAM,
                unionmember = streamPointer
            };
        }

        private static short RegisterFormat(string name)
        {
            uint format = RegisterClipboardFormat(name);

            return unchecked((short)format);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint GlobalAlloc(uint flags, nuint bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint GlobalFree(nint memoryHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint GlobalLock(nint memoryHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(nint memoryHandle);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode,
            EntryPoint = "RegisterClipboardFormatW",
            SetLastError = true)]
        private static extern uint RegisterClipboardFormat(string formatName);

        [DllImport("ole32.dll")]
        private static extern int CreateStreamOnHGlobal(
            nint memoryHandle,
            [MarshalAs(UnmanagedType.Bool)] bool deleteOnRelease,
            out IStream stream);
    }
}
