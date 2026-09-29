using DungeonTable.Core.Dossier;

namespace DungeonTable.Infrastructure.Links;

/// <summary>What an authored link's target turned out to name, as <see cref="LinkTargets"/> resolved it.</summary>
public sealed class LinkTarget
{
    /// <summary>
    /// The id to open: an area's or stat block's node id, or an NPC's or player character's roster id.
    /// </summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>What the target is, for the link's colour and for where clicking it goes.</summary>
    public CrossRefKind Kind { get; init; } = CrossRefKind.None;

    /// <summary>
    /// The words a <c>[[target]]</c> written without shown text puts on the page: an area as written
    /// ("area 6d"), a person by name ("Wenna Brask"), a stat block by its name in the case the
    /// target was written in ("gargoyle", or "Gargoyle" for <c>[[Gargoyle]]</c>).
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// True for a stat block or a spell: a rulebook entry every creature of its kind shares, rather
    /// than one particular somebody. A relation may not name one.
    /// </summary>
    public bool Rulebook { get; init; }

    /// <summary>
    /// True when the target can stand in a room, and so be one of an area's creatures: a stat block,
    /// an individual creature or person, or a player character. Not a faction, an item, a spell or
    /// an area.
    /// </summary>
    public bool CanBeInARoom =>
        TargetId.Length > 0 && Kind is CrossRefKind.Monster or CrossRefKind.Npc or CrossRefKind.Character;
}
