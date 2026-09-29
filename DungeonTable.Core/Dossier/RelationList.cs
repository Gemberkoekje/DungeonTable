namespace DungeonTable.Core.Dossier;

/// <summary>
/// The campaign-wide relations, <c>data/dossiers/relations.json</c>: the ones no single floor owns,
/// such as the party's contacts, what an earlier adventure left behind, and threads that run between
/// levels (a debt run up on one floor and called in on another). A level's own relations live in its
/// level file.
/// </summary>
public sealed class RelationList
{
    /// <summary>The relations, in the order they were written.</summary>
    public IReadOnlyList<Relation> Relations { get; init; } = Array.Empty<Relation>();
}
