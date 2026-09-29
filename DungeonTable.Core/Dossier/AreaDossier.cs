namespace DungeonTable.Core.Dossier;

/// <summary>
/// The prose dossier for a single keyed area, sliced and cleaned from the source book text: the
/// readable prose (read-aloud, at-a-glance features, expanded detail, ability checks), and the
/// creatures and exits the <c>RoomBriefing</c> is built from.
/// </summary>
public sealed class AreaDossier
{
    /// <summary>The area's key number as printed on the map ("1" for area 1; sub-areas keep their letter in <see cref="Title"/>).</summary>
    public int AreaNumber { get; init; }

    /// <summary>The area title ("Undercroft Landing", "Goblin Den").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The area's node id ("data_dossiers_level_1_area_1"): what regions, links and saved state name it by.</summary>
    public string AreaNodeId { get; init; } = string.Empty;

    /// <summary>Source page(s) the dossier was taken from, for provenance ("15", "20-21").</summary>
    public string Pages { get; init; } = string.Empty;

    /// <summary>Boxed read-aloud / boxed text, safe to read to the players. Empty when the area has none.</summary>
    public string ReadAloud { get; init; } = string.Empty;

    /// <summary>The boldface at-a-glance feature bullets, in the book's order of prominence.</summary>
    public IReadOnlyList<DossierBlock> Glance { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>The expanded detail subsections, opened only when the party engages with a feature.</summary>
    public IReadOnlyList<DossierBlock> Detail { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>Ability checks called out in the area, surfaced as quick-reference pills.</summary>
    public IReadOnlyList<SkillCheck> SkillChecks { get; init; } = Array.Empty<SkillCheck>();

    /// <summary>
    /// Who is in the area: its occupants and its encounter. None means nobody is there; a creature
    /// the prose only mentions is not an occupant.
    /// </summary>
    public IReadOnlyList<AreaCreature> Creatures { get; init; } = Array.Empty<AreaCreature>();

    /// <summary>Where the party can go from here: the area's connections. None means no way out is worth naming.</summary>
    public IReadOnlyList<AreaExit> Exits { get; init; } = Array.Empty<AreaExit>();
}
