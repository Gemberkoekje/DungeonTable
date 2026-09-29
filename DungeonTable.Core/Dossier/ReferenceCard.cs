namespace DungeonTable.Core.Dossier;

/// <summary>
/// A link-out reference card for what a cross-reference points at: an entity a level declares, a
/// monster or spell stat block, or an entry in the book index. Shown in the DM panel's reference
/// drawer, the digital form of the book's "bold name, go look it up" convention. The
/// <see cref="Summary"/> body is itself run through the cross-reference engine, so a card can chain
/// to further cards.
/// </summary>
public sealed class ReferenceCard
{
    /// <summary>The id this card describes ("data_statblocks_monsters_bugbear", "bell-warden", "the-old-mill").</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Human-readable label ("Bugbear", "Grask's crew").</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>What kind of thing the card describes, for colouring and the target view.</summary>
    public CrossRefKind Kind { get; init; } = CrossRefKind.None;

    /// <summary>What the DM needs to know about it, in prose; empty when nothing is written.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable source citation ("SRD 5.1 p.266", "A Millbrook Almanac, p. 6"),
    /// or empty when there is no source to cite.
    /// </summary>
    public string Citation { get; init; } = string.Empty;

    /// <summary>
    /// Every authored area that lists or mentions this card's subject, so the card can offer a jump
    /// rather than being a dead end. Empty when none does.
    /// </summary>
    public IReadOnlyList<AreaLink> Areas { get; init; } = Array.Empty<AreaLink>();

    /// <summary>
    /// The node id of the stat block an authored individual uses (the Bell-Warden uses the
    /// Gargoyle's), shown on its card below the description. Empty for a card that is a stat block
    /// itself, and for an entity that fights with none.
    /// </summary>
    public string StatBlockId { get; init; } = string.Empty;

    /// <summary>
    /// The floor whose prose <see cref="Summary"/> belongs to, for an entity declared in a level
    /// file: a bare <c>[[area 6d]]</c> in it means that floor's area 6d, whichever floor is on screen.
    /// Empty for a card that belongs to no floor.
    /// </summary>
    public string LevelNodeId { get; init; } = string.Empty;

    /// <summary>
    /// The relations the card's entity or person is an end of, read from its side ("plots against
    /// Grask"). Empty for a stat block, which is never an end: relations are between individuals.
    /// </summary>
    public IReadOnlyList<RelationLine> Relations { get; init; } = Array.Empty<RelationLine>();
}
