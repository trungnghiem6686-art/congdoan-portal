using CongDoan.Testing;

namespace CongDoan.Shared.Tests;

public class ClockTests
{
    [Fact]
    public void FakeClock_Advance_MovesUtcNow()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock(start);

        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(start.AddMinutes(5), clock.UtcNow);
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }
}
