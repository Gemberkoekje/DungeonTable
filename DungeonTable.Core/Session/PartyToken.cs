namespace DungeonTable.Core.Session;

/// <summary>
/// Where the party stands on the map in play, and how far they see from there: the token the DM drags
/// on their map to reveal what the party sees as it goes. The DM's map only; the projector never
/// shows it.
/// </summary>
/// <remarks>
/// The vision range belongs to the token even while it is off the map, so the DM can set it before
/// placing the party, and it is kept per map with the token, so it no longer goes back to 30 ft on
/// every reload.
/// </remarks>
public sealed class PartyToken
{
    /// <summary>The vision range a token starts with: what the old lifter started with.</summary>
    public const int DefaultVisionFeet = 30;

    /// <summary>The shortest vision range a token can have: one square.</summary>
    public const int MinVisionFeet = 5;

    /// <summary>
    /// The longest vision range a token can have. A huge range would scan a huge tile grid on every
    /// step of a drag and stall the DM's screen.
    /// </summary>
    public const int MaxVisionFeet = 120;

    /// <summary>No token on the map, with the default vision range.</summary>
    public static PartyToken None { get; } = new PartyToken();

    /// <summary>True when the token is on the map.</summary>
    public bool IsPlaced { get; init; }

    /// <summary>Where the token stands, in map world units; meaningless unless <see cref="IsPlaced"/>.</summary>
    public double X { get; init; }

    /// <summary>Where the token stands, in map world units; meaningless unless <see cref="IsPlaced"/>.</summary>
    public double Y { get; init; }

    /// <summary>How far the party sees, in feet (5 ft a square).</summary>
    public int VisionFeet { get; init; } = DefaultVisionFeet;

    /// <summary>
    /// A vision range brought into bounds: anything below one square, or not set, is the default
    /// rather than blindness, and anything past <see cref="MaxVisionFeet"/> is that.
    /// </summary>
    /// <param name="feet">The range asked for.</param>
    /// <returns>The range the token gets.</returns>
    public static int ClampVision(int feet)
    {
        if (feet <= 0)
        {
            return DefaultVisionFeet;
        }

        return Math.Clamp(feet, MinVisionFeet, MaxVisionFeet);
    }
}
