using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.Services.Settings;

public sealed class PromptTextSizeSettingsStateApplicatorTests
{
    [Fact]
    public void Apply_WithRegisteredValue_UpdatesPromptTextSize()
    {
        TestContext context = new();
        double textSize = context.Definition.Options
            .First(option => option.Value > context.Definition.DefaultValue)
            .Value;

        context.Applicator.Apply(context.ValueConverter.Format(textSize));

        context.Service.CurrentTextSize.Should().Be(textSize);
    }

    [Fact]
    public void Apply_WithOpenSettingsEditor_UpdatesSelectedOption()
    {
        TestContext context = new();
        PromptTextSizeSettingViewModelFactory factory = new(
            context.Service,
            new UnusedSettingsStateService(),
            context.ValueConverter,
            new TestViewModelErrorHandler(),
            TestLocalizationTextProvider.Default);
        using NumericSettingViewModel viewModel = factory.Create(context.Definition)
            as NumericSettingViewModel
            ?? throw new InvalidOperationException("Numeric setting view model was not created.");
        double textSize = context.Definition.Options
            .First(option => option.Value > context.Definition.DefaultValue)
            .Value;

        context.Applicator.Apply(context.ValueConverter.Format(textSize));

        viewModel.SelectedOption.Should().NotBeNull();
        viewModel.SelectedOption?.Value.Should().Be(textSize);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("100")]
    public void Apply_WithInvalidValue_KeepsCurrentPromptTextSize(string value)
    {
        TestContext context = new();

        context.Applicator.Apply(value);

        context.Service.CurrentTextSize.Should().Be(context.Definition.DefaultValue);
    }

    private sealed class TestContext
    {
        public PromptTextSizeSettingDefinition Definition { get; }
        public PromptTextSizeService Service { get; }
        public DoubleSettingValueConverter ValueConverter { get; }
        public PromptTextSizeSettingsStateApplicator Applicator { get; }

        public TestContext()
        {
            Definition = new PromptTextSizeSettingDefinition();
            SettingsDefinitionCatalog catalog = new(
                [Definition],
                Array.Empty<IUiScaleOptionDefinition>());
            Service = new PromptTextSizeService(catalog);
            ValueConverter = new DoubleSettingValueConverter();
            Applicator = new PromptTextSizeSettingsStateApplicator(
                catalog,
                Service,
                ValueConverter);
        }
    }

    private sealed class UnusedSettingsStateService : ISettingsStateService
    {
        public Task ApplySavedSettingsAsync(CancellationToken ct)
        {
            throw new NotSupportedException("Applying settings is not used by this test.");
        }

        public void ApplyValue(ISettingsDefinition definition, string value)
        {
            throw new NotSupportedException("Applying a value is not used by this test.");
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
            throw new NotSupportedException("Saving settings is not used by this test.");
        }
    }
}
