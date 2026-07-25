using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class PromptTextSizeControllerTests
{
    [Theory]
    [InlineData(PromptTextSizeAdjustment.Decrease, 13d)]
    [InlineData(PromptTextSizeAdjustment.Increase, 15d)]
    public async Task AdjustAsync_WithAdjacentOption_AppliesAndSavesValue(
        PromptTextSizeAdjustment adjustment,
        double expectedTextSize)
    {
        TestContext context = new();

        await context.Controller.AdjustAsync(adjustment, CancellationToken.None);

        context.PromptTextSizeService.CurrentTextSize.Should().Be(expectedTextSize);
        context.SettingsStateService.AppliedKey.Should().Be(context.Definition.Key);
        context.SettingsStateService.SavedKey.Should().Be(context.Definition.Key);
        context.SettingsStateService.SavedValue.Should()
            .Be(context.ValueConverter.Format(expectedTextSize));
    }

    [Theory]
    [InlineData(PromptTextSizeAdjustment.Decrease, true)]
    [InlineData(PromptTextSizeAdjustment.Increase, false)]
    public async Task AdjustAsync_AtBoundary_DoesNotApplyOrSave(
        PromptTextSizeAdjustment adjustment,
        bool useFirstOption)
    {
        TestContext context = new();
        double boundaryValue = useFirstOption
            ? context.Definition.Options[0].Value
            : context.Definition.Options[^1].Value;
        context.PromptTextSizeService.SetTextSize(boundaryValue);

        await context.Controller.AdjustAsync(adjustment, CancellationToken.None);

        context.SettingsStateService.AppliedKey.Should().BeNull();
        context.SettingsStateService.SavedKey.Should().BeNull();
    }

    [Fact]
    public async Task AdjustAsync_WithOpenSettingsEditor_UpdatesSelectedOption()
    {
        TestContext context = new();
        PromptTextSizeSettingViewModelFactory factory = new(
            context.PromptTextSizeService,
            context.SettingsStateService,
            context.ValueConverter,
            new TestViewModelErrorHandler());
        using NumericSettingViewModel viewModel = factory.Create(context.Definition)
            as NumericSettingViewModel
            ?? throw new InvalidOperationException("Numeric setting view model was not created.");

        await context.Controller.AdjustAsync(
            PromptTextSizeAdjustment.Increase,
            CancellationToken.None);

        viewModel.SelectedOption.Should().NotBeNull();
        viewModel.SelectedOption?.Value.Should()
            .Be(context.PromptTextSizeService.CurrentTextSize);
    }

    private sealed class TestContext
    {
        public PromptTextSizeSettingDefinition Definition { get; }
        public PromptTextSizeService PromptTextSizeService { get; }
        public DoubleSettingValueConverter ValueConverter { get; }
        public RecordingSettingsStateService SettingsStateService { get; }
        public PromptTextSizeController Controller { get; }

        public TestContext()
        {
            Definition = new PromptTextSizeSettingDefinition();
            SettingsDefinitionCatalog catalog = new(
                [Definition],
                Array.Empty<IUiScaleOptionDefinition>());
            PromptTextSizeService = new PromptTextSizeService(catalog);
            ValueConverter = new DoubleSettingValueConverter();
            PromptTextSizeSettingsStateApplicator applicator = new(
                catalog,
                PromptTextSizeService,
                ValueConverter);
            SettingsStateService = new RecordingSettingsStateService(applicator);
            Controller = new PromptTextSizeController(
                catalog,
                PromptTextSizeService,
                SettingsStateService,
                ValueConverter);
        }
    }

    private sealed class RecordingSettingsStateService : ISettingsStateService
    {
        public string? AppliedKey { get; private set; }
        public string? SavedKey { get; private set; }
        public string? SavedValue { get; private set; }

        private readonly ISettingsStateApplicator _applicator;

        public RecordingSettingsStateService(ISettingsStateApplicator applicator)
        {
            _applicator = applicator ?? throw new ArgumentNullException(nameof(applicator));
        }

        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            throw new NotSupportedException("Applying saved settings is not used by this test.");
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
            AppliedKey = definition.Key;
            _applicator.Apply(value);
        }

        public Task<string?> LoadValueAsync(ISettingsDefinition definition, CancellationToken ct)
        {
            throw new NotSupportedException("Loading settings is not used by this test.");
        }

        public Task SaveValueAsync(
            ISettingsDefinition definition,
            string value,
            CancellationToken ct)
        {
            SavedKey = definition.Key;
            SavedValue = value;

            return Task.CompletedTask;
        }
    }
}
