namespace DungeonTable.Core.Dossier;

/// <summary>
/// One cross-connection between two people, factions or things the book makes hard to see: who is
/// at odds with whom, who serves whom, who fears what. A level's relations live in its level file;
/// campaign-wide ones in <c>relations.json</c>.
/// </summary>
/// <remarks>
/// Both ends are ids, named the way a link names its target: an entity or NPC id, a player
/// character's id, or an area (<c>area 3a</c>, <c>L2 area 1</c>). Never a stat block: a relation is
/// between individuals, and the gargoyle that guards the nave is <c>bell-warden</c>, not every
/// gargoyle there is.
/// </remarks>
public sealed class Relation
{
    /// <summary>The first end ("nib").</summary>
    public string A { get; init; } = string.Empty;

    /// <summary>The kind, one of <see cref="RelationKinds.Authored"/> ("rival", "plots-against").</summary>
    public string Rel { get; init; } = string.Empty;

    /// <summary>The second end ("grask").</summary>
    public string B { get; init; } = string.Empty;

    /// <summary>What the kind cannot say ("would sell him to the party for the den"), in prose.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>True when the players do not know of it.</summary>
    public bool Secret { get; init; }
}
