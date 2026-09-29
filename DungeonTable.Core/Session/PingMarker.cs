namespace DungeonTable.Core.Session;

/// <summary>
/// Where the DM last pointed on the projector, in map world units. The DM's finger, in effect: a
/// spot the players' eyes are meant to go to right now.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Sequence"/> is what makes a ping repeatable. The projector shows the ping as an
/// animation that runs and then fades, and an animation is only re-triggered when its element is
/// recreated — so pointing twice at the same spot has to look different from a re-render of the same
/// state. A number that only ever goes up gives the player view a key it can hang the animation on.
/// </para>
/// <para>
/// Deliberately <b>not persisted</b> with the rest of the table. A ping means "look here, now"; one
/// restored from last Tuesday would be a stale hand pointing at nothing.
/// </para>
/// </remarks>
public sealed class PingMarker
{
    /// <summary>No ping (nothing has been pointed at, or the DM cleared it).</summary>
    public static PingMarker None { get; } = new PingMarker();

    /// <summary>The point on the map, in world units.</summary>
    public double X { get; init; }

    /// <summary>The point on the map, in world units.</summary>
    public double Y { get; init; }

    /// <summary>Which ping this is; 0 means none has been placed.</summary>
    public long Sequence { get; init; }

    /// <summary>True when there is a ping to draw.</summary>
    public bool IsSet => Sequence > 0;
}
