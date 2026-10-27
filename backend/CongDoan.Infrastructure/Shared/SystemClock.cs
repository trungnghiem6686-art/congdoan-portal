using CongDoan.Application.Shared;

namespace CongDoan.Infrastructure.Shared;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
