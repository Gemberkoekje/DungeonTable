using DungeonTable.Core.Dossier;

namespace DungeonTable.Core.Briefing;

/// <summary>
/// Who one of a room's occupants is involved with: "Nib — plots against Grask (4)".
/// The briefing's answer to the cross-connections the book makes hard to see from inside one room.
/// </summary>
public sealed class Involvement
{
    /// <summary>The occupant's id.</summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary>The occupant's name.</summary>
    public string SubjectLabel { get; init; } = string.Empty;

    /// <summary>The occupant's relations, read from the occupant's side.</summary>
    public IReadOnlyList<RelationLine> Lines { get; init; } = Array.Empty<RelationLine>();
}
