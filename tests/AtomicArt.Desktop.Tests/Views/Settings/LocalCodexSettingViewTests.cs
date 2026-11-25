using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Controls.Settings;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class LocalCodexSettingViewTests : DesktopControlTestBase
{
    [Theory]
    [InlineData("")]
    [InlineData("  Первая строка.\n\nSecond line.  \n")]
    public async Task SystemPrompt_WhenEditorLosesFocus_SavesExactTextAutomatically(string prompt)
    {
        await DispatchAsync(async () =>
        {
            LocalCodexSettings settings = new();
            settings.ApplySerializedValue(JsonSerializer.Serialize(settings.Current with { SystemPrompt = "Original instructions" }));
            Mock<ISettingsStateService> state = new();
            state.Setup(service => service.SaveValueAsync(It.IsAny<ISettingsDefinition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            state.Setup(service => service.ApplyValue(It.IsAny<ISettingsDefinition>(), It.IsAny<string>()))
                .Callback((ISettingsDefinition _, string value) => settings.ApplySerializedValue(value));
            using LocalCodexSettingViewModel viewModel = new(new LocalCodexSettingDefinition(), state.Object, settings,
                new LocalCodexAppServer(NullLogger<LocalCodexAppServer>.Instance),
                new TestViewModelErrorHandler(), TestLocalizationTextProvider.Default);
            LocalCodexSettingView view = new() { DataContext = viewModel };
            TextBlock referenceLabel = new() { Text = "Existing setting", Classes = { "settings-label" } };
            TextBox referenceEditor = new();
            SettingItemControl reference = new() { DataContext = viewModel, Header = referenceLabel, Editor = referenceEditor };
            StackPanel content = new() { Children = { reference, view } };
            Window window = Show(content);

            try
            {
                TextBox editor = view.GetVisualDescendants().OfType<TextBox>().Single(text => text.AcceptsReturn);
                TextBox folder = view.GetVisualDescendants().OfType<TextBox>().Single(text => !text.AcceptsReturn);
                editor.Text.Should().Be(settings.Current.SystemPrompt);
                editor.IsEffectivelyVisible.Should().BeTrue();
                editor.Bounds.Height.Should().BeGreaterThan(folder.Bounds.Height);
                editor.Bounds.Width.Should().BeApproximately(view.Bounds.Width, 1d);
                TextBlock label = view.GetVisualDescendants().OfType<TextBlock>()
                    .Single(block => (block.Classes.Contains("settings-label") || block.Classes.Contains("settings-description"))
                        && block.IsEffectivelyVisible
                        && !block.GetVisualAncestors().OfType<CheckBox>().Any());
                double expectedGap = (referenceEditor.TranslatePoint(default, referenceLabel)
                    ?? throw new InvalidOperationException("The reference editor has no position.")).Y - referenceLabel.Bounds.Height;
                double actualGap = (editor.TranslatePoint(default, label)
                    ?? throw new InvalidOperationException("The custom prompt editor has no position.")).Y - label.Bounds.Height;
                actualGap.Should().BeApproximately(expectedGap, 0.01d);
                label.FontWeight.Should().Be(referenceLabel.FontWeight);
                label.Foreground.Should().BeSameAs(referenceLabel.Foreground);
                editor.Focus();

                editor.Text = prompt;
                folder.Focus();
                await (viewModel.CommitCommand.ExecutionTask
                    ?? throw new InvalidOperationException("Leaving the custom system prompt must save the setting."));

                viewModel.SystemPrompt.Should().Be(prompt);
                settings.Current.SystemPrompt.Should().Be(prompt);
                string expectedValue = JsonSerializer.Serialize(settings.Current);
                state.Verify(service => service.SaveValueAsync(It.IsAny<ISettingsDefinition>(),
                    expectedValue,
                    It.IsAny<CancellationToken>()), Times.Once);
                viewModel.HasErrorMessage.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
