using CongDoan.Application.Shared;

namespace CongDoan.Testing;

public sealed class FakeClock : IClock
{
    private DateTime _now;

    public FakeClock(DateTime? start = null) =>
        _now = DateTime.SpecifyKind(start ?? new DateTime(2026, 1, 1, 0, 0, 0), DateTimeKind.Utc);

    public DateTime UtcNow => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);

    public void Set(DateTime utc) => _now = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
}
