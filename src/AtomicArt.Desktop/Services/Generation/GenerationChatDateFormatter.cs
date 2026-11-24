using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationChatDateFormatter
{
    public event EventHandler? Changed
    {
        add => _localization.Changed += value;
        remove => _localization.Changed -= value;
    }

    private readonly ILocalizationTextProvider _text;
    private readonly ILocalizationService _localization;
    private readonly TimeProvider _clock;

    public GenerationChatDateFormatter(
        ILocalizationTextProvider text, ILocalizationService localization, TimeProvider clock)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public DateOnly GetDate(DateTime createdAtUtc)
    {
        return DateOnly.FromDateTime(GetLocalTime(createdAtUtc));
    }

    public string Format(DateTime createdAtUtc)
    {
        DateTime local = GetLocalTime(createdAtUtc);
        string key = local.Year == _clock.GetLocalNow().Year
            ? GenerationChatLocalizationKeys.Date
            : GenerationChatLocalizationKeys.DateWithYear;

        return string.Format(_localization.CurrentCulture, _text.Get(key), local);
    }

    private DateTime GetLocalTime(DateTime createdAtUtc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc), _clock.LocalTimeZone);
    }
}
