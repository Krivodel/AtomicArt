using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationItemStatusDescriptorRegistryTests
{
    private const int UnknownStatusValue = 999;

    [Fact]
    public void Get_WithGeneratedStatus_ReturnsGeneratedDescriptor()
    {
        IGenerationItemStatusDescriptorRegistry registry =
            GenerationItemStatusDescriptorRegistryTestFactory.Create();

        IGenerationItemStatusDescriptor descriptor = registry.Get(GenerationItemStatus.Generated);

        descriptor.Should().BeOfType<GeneratedGenerationItemStatusDescriptor>();
    }

    [Fact]
    public void Get_WithGeneratingStatus_ReturnsGeneratingDescriptor()
    {
        IGenerationItemStatusDescriptorRegistry registry =
            GenerationItemStatusDescriptorRegistryTestFactory.Create();

        IGenerationItemStatusDescriptor descriptor = registry.Get(GenerationItemStatus.Generating);

        descriptor.Should().BeOfType<GeneratingGenerationItemStatusDescriptor>();
    }

    [Fact]
    public void Get_WithFailedStatus_ReturnsFailedDescriptor()
    {
        IGenerationItemStatusDescriptorRegistry registry =
            GenerationItemStatusDescriptorRegistryTestFactory.Create();

        IGenerationItemStatusDescriptor descriptor = registry.Get(GenerationItemStatus.Failed);

        descriptor.Should().BeOfType<FailedGenerationItemStatusDescriptor>();
    }

    [Fact]
    public void Get_WithUnknownStatus_ReturnsUnknownDescriptorFromFactory()
    {
        GenerationItemStatus unknownStatus = (GenerationItemStatus)UnknownStatusValue;
        RecordingUnknownGenerationItemStatusDescriptorFactory factory = new();
        GenerationItemStatusDescriptorRegistry registry = new(
            GenerationItemStatusDescriptorRegistryTestFactory.CreateRegisteredDescriptors(),
            factory);

        IGenerationItemStatusDescriptor descriptor = registry.Get(unknownStatus);

        factory.RequestedStatus.Should().Be(unknownStatus);
        descriptor.Should().BeSameAs(factory.CreatedDescriptor);
        descriptor.Should().BeOfType<UnknownGenerationItemStatusDescriptor>();
        descriptor.Status.Should().Be(unknownStatus);
        descriptor.DisplayText.Should().Be(unknownStatus.ToString());
        descriptor.VisualState.Should().Be(GenerationItemVisualState.Unknown);
        descriptor.ResultContentPolicy.Should().Be(GenerationResultContentPolicy.Ignore);
    }

    private sealed class RecordingUnknownGenerationItemStatusDescriptorFactory
        : IUnknownGenerationItemStatusDescriptorFactory
    {
        public GenerationItemStatus? RequestedStatus { get; private set; }
        public IGenerationItemStatusDescriptor? CreatedDescriptor { get; private set; }

        public IGenerationItemStatusDescriptor Create(GenerationItemStatus status)
        {
            IGenerationItemStatusDescriptor descriptor = new UnknownGenerationItemStatusDescriptor(status);

            RequestedStatus = status;
            CreatedDescriptor = descriptor;

            return descriptor;
        }
    }
}
