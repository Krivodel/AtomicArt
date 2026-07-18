using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class DesktopModelPanelRegistryTests
{
    [Fact]
    public void GetDefaultPanel_WithPanels_ReturnsFirstPanel()
    {
        DesktopModelPanelRegistry registry = new();
        TestModelPanel firstPanel = new("first-model");
        TestModelPanel secondPanel = new("second-model");
        IModelPanelViewModel[] panels = [firstPanel, secondPanel];

        IModelPanelViewModel panel = registry.GetDefaultPanel(panels);

        panel.Should().BeSameAs(firstPanel);
    }

    [Fact]
    public void GetPanel_WhenSinglePanelSupportsMultipleModels_ReturnsSharedPanel()
    {
        DesktopModelPanelRegistry registry = new();
        TestModelPanel sharedPanel = new("first-model", "second-model");
        IModelPanelViewModel[] panels = [sharedPanel];

        IModelPanelViewModel firstPanel = registry.GetPanel("first-model", panels);
        IModelPanelViewModel secondPanel = registry.GetPanel("second-model", panels);

        firstPanel.Should().BeSameAs(sharedPanel);
        secondPanel.Should().BeSameAs(sharedPanel);
    }

    [Fact]
    public void GetPanel_WhenNoPanelSupportsModel_ThrowsInvalidOperationException()
    {
        DesktopModelPanelRegistry registry = new();
        IModelPanelViewModel[] panels = [new TestModelPanel("first-model")];

        Action act = () => registry.GetPanel("second-model", panels);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Desktop panel is not registered for selected model.");
    }

    private sealed class TestModelPanel : IModelPanelViewModel
    {
        public string PanelId => "test-panel";
        public string ModelId => _supportedModelIds[0];
        public string DisplayName => "Test Model";
        public int MaxAttachedImageBytes => 1024;
        public int AttachmentInputByteLimit => 8192;
        public IAsyncRelayCommand GenerateCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
        public IAsyncRelayCommand PickImageCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
        public IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> AttachImagesCommand { get; } =
            new AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>(_ => Task.CompletedTask);
        public IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?> AttachImageInputsCommand { get; } =
            new AsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>(_ => Task.CompletedTask);

        private readonly IReadOnlyList<string> _supportedModelIds;

        public TestModelPanel(params string[] supportedModelIds)
        {
            ArgumentNullException.ThrowIfNull(supportedModelIds);

            if (supportedModelIds.Length == 0)
            {
                throw new ArgumentException(
                    "At least one supported model id is required.",
                    nameof(supportedModelIds));
            }

            _supportedModelIds = supportedModelIds;
        }

        public bool SupportsModel(string modelId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

            return _supportedModelIds.Contains(modelId, StringComparer.Ordinal);
        }
    }
}
