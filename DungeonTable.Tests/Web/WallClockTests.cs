using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The DM screen's "Saved 14:26" is read against the clock on the wall. The server renders it, and in a
/// container the server's clock is UTC, so the time is shown at the browser's distance from UTC.
/// </summary>
public sealed class WallClockTests
{
    private static readonly DateTimeOffset SavedAt = new(2026, 9, 29, 12, 26, 12, TimeSpan.Zero);

    [Theory]
    [InlineData(120, "14:26")]
    [InlineData(0, "12:26")]
    [InlineData(-300, "07:26")]
    [InlineData(330, "17:56")]
    public void A_save_time_reads_as_the_browsers_clock_does(int browserMinutes, string expected)
    {
        Assert.True(WallClock.TryOffset(browserMinutes, out TimeSpan offset));

        Assert.Equal(expected, WallClock.HoursMinutes(SavedAt, offset));
    }

    [Fact]
    public void A_save_just_before_midnight_in_utc_reads_as_after_midnight_east_of_it()
    {
        var late = new DateTimeOffset(2026, 9, 29, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal("01:30", WallClock.HoursMinutes(late, TimeSpan.FromHours(2)));
    }

    [Theory]
    [InlineData(840)]
    [InlineData(-720)]
    public void The_zones_furthest_from_utc_are_read(int browserMinutes)
    {
        Assert.True(WallClock.TryOffset(browserMinutes, out TimeSpan offset));
        Assert.Equal(TimeSpan.FromMinutes(browserMinutes), offset);
    }

    [Theory]
    [InlineData(841)]
    [InlineData(-841)]
    [InlineData(int.MaxValue)]
    public void A_distance_no_time_zone_has_is_refused(int browserMinutes)
    {
        Assert.False(WallClock.TryOffset(browserMinutes, out _));
    }
}
