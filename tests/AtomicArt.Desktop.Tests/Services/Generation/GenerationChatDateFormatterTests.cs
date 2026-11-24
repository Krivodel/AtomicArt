using System.Globalization;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationChatDateFormatterTests
{
    [Theory]
    [InlineData(2026, "24 ноября")]
    [InlineData(2025, "24 ноября 2025 г.")]
    public void Format_WithRussianDate_ShowsYearOnlyForOtherYears(int year, string expected)
    {
        Mock<ILocalizationService> localization = new();
        localization.SetupGet(service => service.CurrentCulture).Returns(CultureInfo.GetCultureInfo("ru-RU"));
        Dictionary<string, string> strings = new Dictionary<string, string>
        {
            [GenerationChatLocalizationKeys.Date] = "{0:d MMMM}",
            [GenerationChatLocalizationKeys.DateWithYear] = "{0:d MMMM yyyy 'г.'}"
        };
        Mock<TimeProvider> clock = new();
        clock.Setup(provider => provider.GetUtcNow()).Returns(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        clock.SetupGet(provider => provider.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        GenerationChatDateFormatter formatter = new(new TestLocalizationTextProvider(strings), localization.Object, clock.Object);

        string actual = formatter.Format(new DateTime(year, 11, 24));

        actual.Should().Be(expected);
    }

    [Fact]
    public void GetDate_WithUtcNearMidnight_UsesLocalDate()
    {
        Mock<TimeProvider> clock = new();
        clock.SetupGet(provider => provider.LocalTimeZone).Returns(
            TimeZoneInfo.CreateCustomTimeZone("Test UTC+3", TimeSpan.FromHours(3), "Test UTC+3", "Test UTC+3"));
        GenerationChatDateFormatter formatter = new(
            TestLocalizationTextProvider.Default, Mock.Of<ILocalizationService>(), clock.Object);

        DateOnly date = formatter.GetDate(new DateTime(2026, 11, 23, 22, 0, 0));

        date.Should().Be(new DateOnly(2026, 11, 24));
    }
}
