namespace DungeonTable.Core.Dossier;

/// <summary>
/// What an authored <see cref="Entity"/> is: an individual creature, a named person, a faction, or an
/// item that matters. It decides how a link to the entity is coloured and how its card reads.
/// </summary>
public enum EntityKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>
    /// One particular creature that uses a stat block without being it: the Bell-Warden, not
    /// the Gargoyle entry every gargoyle in the dungeon shares.
    /// </summary>
    Creature = 1,

    /// <summary>A named person who belongs to one floor, such as Grask on Level 1.</summary>
    Npc = 2,

    /// <summary>A faction or organisation (Grask's crew, the Wickfoot goblins).</summary>
    Faction = 3,

    /// <summary>An item that matters to the story, beyond its entry in the rulebook.</summary>
    Item = 4,
}
