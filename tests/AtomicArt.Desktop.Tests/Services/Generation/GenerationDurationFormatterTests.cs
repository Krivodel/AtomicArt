using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationDurationFormatterTests
{
    private readonly GenerationDurationFormatter _formatter =
        new(TestLocalizationTextProvider.Default);

    [Fact]
    public void Format_WithSeconds_ReturnsSecondsText()
    {
        string? result = _formatter.Format(TimeSpan.FromSeconds(30));

        result.Should().Be(string.Concat(
            "30",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.SecondShort)));
    }

    [Fact]
    public void Format_WithMinutes_ReturnsMinutesSecondsText()
    {
        string? result = _formatter.Format(TimeSpan.FromSeconds(150));

        result.Should().Be(string.Concat(
            "2",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.MinuteShort),
            ":30",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.SecondShort)));
    }

    [Fact]
    public void Format_WithHours_ReturnsHoursMinutesSecondsText()
    {
        string? result = _formatter.Format(TimeSpan.FromSeconds(7410));

        result.Should().Be(string.Concat(
            "2",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.HourShort),
            ":3",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.MinuteShort),
            ":30",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.SecondShort)));
    }

    [Fact]
    public void Format_WithMissingDuration_ReturnsNull()
    {
        string? result = _formatter.Format(null);

        result.Should().BeNull();
    }
}
