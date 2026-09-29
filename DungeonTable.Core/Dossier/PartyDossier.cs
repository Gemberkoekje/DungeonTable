namespace DungeonTable.Core.Dossier;

/// <summary>
/// The fifth dossier tier, beside the per-area, per-floor, shared-rules and campaign ones: the
/// party itself. The characters the DM is running the dungeon <em>for</em> — their numbers, the
/// abilities that reshape an encounter, and the threads their players wrote.
/// </summary>
/// <remarks>
/// Campaign-scoped rather than per-level: a character's backstory is equally relevant on floor one
/// and floor twenty, so this loads once with the quest log and the campaign background rather than
/// on every navigation.
/// </remarks>
public sealed class PartyDossier
{
    /// <summary>The party's display title ("The party at a glance").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>What the party's composition means for how the dungeon should be run.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>The characters, in the order they should be shown.</summary>
    public IReadOnlyList<CharacterDossier> Characters { get; init; } = Array.Empty<CharacterDossier>();

    /// <summary>
    /// Party-wide notes that belong to no single character: what the composition does to encounter
    /// design, and the threads that tie two players' backstories together.
    /// </summary>
    public IReadOnlyList<DossierBlock> Notes { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>Things to settle with the players before the next session.</summary>
    public IReadOnlyList<DossierBlock> OpenQuestions { get; init; } = Array.Empty<DossierBlock>();
}
