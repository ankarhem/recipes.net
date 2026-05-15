using Domain;

namespace Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; // ast-grep-ignore: no-utc-now
}
