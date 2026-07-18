namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationDurationFormatter
{
    public string? Format(TimeSpan? duration)
    {
        if (duration is null)
        {
            return null;
        }

        int totalSeconds = Math.Max(0, (int)Math.Floor(duration.Value.TotalSeconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours}ч:{minutes}м:{seconds}с";
        }

        if (minutes > 0)
        {
            return $"{minutes}м:{seconds}с";
        }

        return $"{seconds}с";
    }
}
