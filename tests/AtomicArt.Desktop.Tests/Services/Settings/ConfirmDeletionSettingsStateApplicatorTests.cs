using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Tests.Services.Settings;

public sealed class ConfirmDeletionSettingsStateApplicatorTests
{
    [Fact]
    public void Apply_WithBooleanValue_UpdatesConfirmationRequirement()
    {
        TestContext context = new();

        context.Applicator.Apply(
            context.ValueConverter.Format(!context.Definition.DefaultValue));

        context.Service.IsConfirmationRequired.Should().Be(
            !context.Definition.DefaultValue);
    }

    [Fact]
    public void Apply_WithInvalidValue_KeepsCurrentConfirmationRequirement()
    {
        TestContext context = new();

        context.Applicator.Apply("invalid");

        context.Service.IsConfirmationRequired.Should().Be(
            context.Definition.DefaultValue);
    }

    private sealed class TestContext
    {
        public ConfirmDeletionSettingDefinition Definition { get; }
        public DeletionConfirmationService Service { get; }
        public BooleanSettingValueConverter ValueConverter { get; }
        public ConfirmDeletionSettingsStateApplicator Applicator { get; }

        public TestContext()
        {
            Definition = new ConfirmDeletionSettingDefinition();
            SettingsDefinitionCatalog catalog = new(
                [Definition],
                Array.Empty<IUiScaleOptionDefinition>());
            Service = new DeletionConfirmationService(catalog);
            ValueConverter = new BooleanSettingValueConverter();
            Applicator = new ConfirmDeletionSettingsStateApplicator(
                catalog,
                Service,
                ValueConverter);
        }
    }
}
