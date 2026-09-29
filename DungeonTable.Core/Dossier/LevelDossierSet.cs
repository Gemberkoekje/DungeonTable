namespace DungeonTable.Core.Dossier;

/// <summary>
/// The on-disk shape of a single <c>level-{n}.json</c> dossier file: one level dossier plus all of
/// its area dossiers. The file-system store deserializes this and indexes the areas by node id.
/// </summary>
public sealed class LevelDossierSet
{
    /// <summary>The floor-level dossier.</summary>
    public LevelDossier Level { get; init; } = new LevelDossier();

    /// <summary>The keyed-area dossiers on this level.</summary>
    public IReadOnlyList<AreaDossier> Areas { get; init; } = Array.Empty<AreaDossier>();
}
