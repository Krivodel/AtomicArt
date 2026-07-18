using AtomicArt.Application.Common.Interfaces;

namespace AtomicArt.Infrastructure;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
