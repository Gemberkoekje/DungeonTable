namespace DungeonTable.Web.Services;

/// <summary>What one badge beside a room's label says (see <see cref="RoomBadges"/>).</summary>
public enum BadgeKind
{
    /// <summary>Unset.</summary>
    None = 0,

    /// <summary>Creatures not yet ticked off, with how many.</summary>
    Creatures = 1,

    /// <summary>Every creature the area lists is ticked off.</summary>
    Cleared = 2,

    /// <summary>Something to be found: a block tagged loot.</summary>
    Loot = 3,

    /// <summary>A trap or a hazard the map marks.</summary>
    Hazard = 4,

    /// <summary>A secret way: a secret door in the room's walls, a marker for one, or an exit the players do not know of.</summary>
    Secret = 5,

    /// <summary>The DM wrote a note on the room.</summary>
    Note = 6,
}
