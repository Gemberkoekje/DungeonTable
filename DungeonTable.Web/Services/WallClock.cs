using System.Globalization;

namespace DungeonTable.Web.Services;

/// <summary>
/// A moment as the clock on the DM's wall reads it.
/// </summary>
/// <remarks>
/// The DM screen renders on the server, and the server's clock is UTC in a container, or another zone
/// entirely when the app is hosted somewhere else. So a time the DM reads there is shifted by the
/// browser's own distance from UTC, which the page asks the browser for once it is live
/// (<c>wwwroot/js/dt-clock.js</c>).
/// </remarks>
public static class WallClock
{
    // No time zone in use is further than 14 hours from UTC (the Line Islands are +14).
    private static readonly TimeSpan Furthest = TimeSpan.FromHours(14);

    /// <summary>Reads the distance from UTC a browser reported.</summary>
    /// <param name="minutes">Minutes east of UTC, as <c>dtClock.utcOffsetMinutes</c> reports them.</param>
    /// <param name="offset">The distance from UTC, when a time zone can have it.</param>
    /// <returns>Whether a time zone can be that far from UTC.</returns>
    public static bool TryOffset(int minutes, out TimeSpan offset)
    {
        offset = TimeSpan.FromMinutes(minutes);
        return offset.Duration() <= Furthest;
    }

    /// <summary>The hour and minute of a moment, on a clock at a given distance from UTC.</summary>
    /// <param name="moment">The moment.</param>
    /// <param name="offset">The clock's distance from UTC.</param>
    /// <returns>The time as <c>HH:mm</c>, on a 24-hour clock.</returns>
    public static string HoursMinutes(DateTimeOffset moment, TimeSpan offset) =>
        moment.ToOffset(offset).ToString("HH:mm", CultureInfo.InvariantCulture);
}
