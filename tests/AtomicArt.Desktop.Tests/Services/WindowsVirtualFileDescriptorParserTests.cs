using System.Text;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Windows;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class WindowsVirtualFileDescriptorParserTests
{
    [Fact]
    public void Parse_WithUnicodeDescriptor_ReturnsNameAndSize()
    {
        byte[] data = WindowsVirtualFileTestData.CreateDescriptorGroup(
            WindowsVirtualFileTestData.UnicodeDescriptorSize,
            "пример.png",
            Encoding.Unicode,
            declaredSize: 1234);

        IReadOnlyList<WindowsVirtualFileDescriptor> descriptors =
            WindowsVirtualFileDescriptorParser.Parse(
                data,
                isUnicode: true,
                TestApiConfiguration
                    .CreateDataTransferOptions()
                    .MaximumVirtualFileCount);

        WindowsVirtualFileDescriptor descriptor = descriptors.Should()
            .ContainSingle()
            .Subject;
        descriptor.FileName.Should().Be("пример.png");
        descriptor.DeclaredSize.Should().Be(1234);
        descriptor.IsDirectory.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithAnsiDescriptor_ReturnsName()
    {
        byte[] data = WindowsVirtualFileTestData.CreateDescriptorGroup(
            WindowsVirtualFileTestData.AnsiDescriptorSize,
            "image.png",
            Encoding.ASCII,
            declaredSize: null);

        IReadOnlyList<WindowsVirtualFileDescriptor> descriptors =
            WindowsVirtualFileDescriptorParser.Parse(
                data,
                isUnicode: false,
                TestApiConfiguration
                    .CreateDataTransferOptions()
                    .MaximumVirtualFileCount);

        descriptors.Should().ContainSingle()
            .Which.FileName.Should().Be("image.png");
    }

    [Fact]
    public void Parse_WithDirectoryAttribute_MarksDirectory()
    {
        byte[] data = WindowsVirtualFileTestData.CreateDescriptorGroup(
            WindowsVirtualFileTestData.UnicodeDescriptorSize,
            "folder",
            Encoding.Unicode,
            declaredSize: null,
            isDirectory: true);

        IReadOnlyList<WindowsVirtualFileDescriptor> descriptors =
            WindowsVirtualFileDescriptorParser.Parse(
                data,
                isUnicode: true,
                TestApiConfiguration
                    .CreateDataTransferOptions()
                    .MaximumVirtualFileCount);

        descriptors.Should().ContainSingle()
            .Which.IsDirectory.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithIncompleteDescriptor_ThrowsInvalidDataException()
    {
        byte[] data = new byte[sizeof(uint) + 10];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data, 1);

        Action act = () => WindowsVirtualFileDescriptorParser.Parse(
            data,
            isUnicode: true,
            TestApiConfiguration
                .CreateDataTransferOptions()
                .MaximumVirtualFileCount);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parse_WithExcessiveItemCount_ThrowsInvalidDataException()
    {
        byte[] data = new byte[sizeof(uint)];
        int maximumFileCount = TestApiConfiguration
            .CreateDataTransferOptions()
            .MaximumVirtualFileCount;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            data,
            checked((uint)(maximumFileCount + 1)));

        Action act = () => WindowsVirtualFileDescriptorParser.Parse(
            data,
            isUnicode: true,
            maximumFileCount);

        act.Should().Throw<InvalidDataException>();
    }
}
