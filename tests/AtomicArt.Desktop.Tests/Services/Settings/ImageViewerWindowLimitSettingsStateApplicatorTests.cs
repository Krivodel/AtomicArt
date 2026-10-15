using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Tests.Services.Settings;

public sealed class ImageViewerWindowLimitSettingsStateApplicatorTests
{
    [Theory]
    [InlineData("0", false, 3)]
    [InlineData("1", true, 1)]
    [InlineData("3", true, 3)]
    [InlineData("100", true, 100)]
    [InlineData("{\"IsEnabled\":false,\"Count\":7}", false, 7)]
    [InlineData("{\"IsEnabled\":true,\"Count\":12}", true, 12)]
    public async Task ApplySavedSettingsAsync_WithSavedLimit_RestoresValueAndSynchronizesEditor(
        string value, bool expectedEnabled, int expectedCount)
    {
        SettingsState saved = new()
        {
            Values = new Dictionary<string, string>
            {
                [ImageViewerWindowLimitSettingDefinition.KeyValue] = value
            }
        };
        ImageViewerWindowLimitSettingsTestContext context = new(saved);
        using ImageViewerWindowLimitSettingViewModel editor = context.CreateEditor();

        await context.Settings.ApplySavedSettingsAsync(CancellationToken.None);

        context.Limit.Limit.Should().Be(expectedEnabled ? expectedCount : 0);
        editor.Count.Should().Be(expectedCount);
        editor.IsLimitEnabled.Should().Be(expectedEnabled);
        editor.IsInputEnabled.Should().Be(expectedEnabled);
        context.Scheduler.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("101")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"IsEnabled\":false}")]
    [InlineData("{\"Count\":7}")]
    [InlineData("{\"IsEnabled\":false,\"Count\":0}")]
    [InlineData("{\"IsEnabled\":true,\"Count\":1.5}")]
    public void Apply_WithInvalidLimit_PreservesCurrentValue(string value)
    {
        ImageViewerWindowLimitSettingsTestContext context = new(new SettingsState());

        context.Applicator.Apply(value);

        context.Limit.IsEnabled.Should().BeTrue();
        context.Limit.Count.Should().Be(3);
        context.Limit.Limit.Should().Be(3);
    }

    [Fact]
    public async Task IsLimitEnabled_AfterEditingDisablingAndRestarting_RetainsCountAndRestoresLimit()
    {
        ImageViewerWindowLimitSettingsTestContext context = new(new SettingsState());
        using ImageViewerWindowLimitSettingViewModel editor = context.CreateEditor();
        editor.Count = 7;
        await editor.ApplyCommand.ExecuteAsync(null);

        editor.IsLimitEnabled = false;
        await (editor.ApplyCommand.ExecutionTask ?? Task.CompletedTask);
        context.Limit.Limit.Should().Be(0);
        editor.IsInputEnabled.Should().BeFalse();
        SettingsState state = context.Scheduler.SavedState.Should().BeOfType<SettingsState>().Subject;
        ImageViewerWindowLimitSettingsTestContext reopenedContext = new(state);
        await reopenedContext.Settings.ApplySavedSettingsAsync(CancellationToken.None);
        using ImageViewerWindowLimitSettingViewModel reopened = reopenedContext.CreateEditor();
        reopened.Count.Should().Be(7);
        reopened.IsLimitEnabled.Should().BeFalse();
        reopened.IsInputEnabled.Should().BeFalse();
        reopenedContext.Limit.Limit.Should().Be(0);

        reopened.IsLimitEnabled = true;
        await (reopened.ApplyCommand.ExecutionTask ?? Task.CompletedTask);

        reopenedContext.Limit.Limit.Should().Be(7);
        reopened.IsInputEnabled.Should().BeTrue();
        reopened.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1d)]
    [InlineData(0d)]
    [InlineData(101d)]
    [InlineData(1.5)]
    public void Count_WithIncompleteOrInvalidInput_PreservesEffectiveLimitAndSavedState(double? value)
    {
        ImageViewerWindowLimitSettingsTestContext context = new(new SettingsState());
        using ImageViewerWindowLimitSettingViewModel editor = context.CreateEditor();

        editor.Count = value is double number ? (decimal)number : null;

        editor.ApplyCommand.CanExecute(null).Should().BeFalse();
        context.Limit.Limit.Should().Be(3);
        context.Scheduler.CallCount.Should().Be(0);
    }
}
