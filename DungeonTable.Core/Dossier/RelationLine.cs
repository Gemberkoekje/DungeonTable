namespace DungeonTable.Core.Dossier;

/// <summary>
/// One relation as it reads from one of its ends, for a card or a briefing line: "plots against Grask
/// (4) — would sell him to the party for the den." Built from a <see cref="Relation"/> by whoever
/// resolves its ends.
/// </summary>
public sealed class RelationLine
{
    /// <summary>The kind as written ("rival", "plots-against"), for styling.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>How the relation reads from this end ("rival of", "plotted against by").</summary>
    public string Phrase { get; init; } = string.Empty;

    /// <summary>The id to open for the other end: an entity's, a person's, or an area's node id.</summary>
    public string OtherId { get; init; } = string.Empty;

    /// <summary>The other end's name ("Grask").</summary>
    public string OtherLabel { get; init; } = string.Empty;

    /// <summary>What the other end is, for its link's colour.</summary>
    public CrossRefKind OtherKind { get; init; } = CrossRefKind.None;

    /// <summary>
    /// The printed keys of the areas the other end is listed in ("4"), so a line can say where to
    /// find them. Empty when they are in no room.
    /// </summary>
    public IReadOnlyList<string> OtherWhere { get; init; } = Array.Empty<string>();

    /// <summary>The relation's note, in prose.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>The floor the note's prose belongs to, for resolving its links; empty for a campaign-wide one.</summary>
    public string Floor { get; init; } = string.Empty;

    /// <summary>True when the players do not know of it.</summary>
    public bool Secret { get; init; }

    /// <summary>
    /// The NPCs whose contact lists record this relation; empty for one written as a relation. A card
    /// that already shows one of those NPCs' contacts table leaves the line out.
    /// </summary>
    public IReadOnlyList<string> FromContactsOf { get; init; } = Array.Empty<string>();
}
