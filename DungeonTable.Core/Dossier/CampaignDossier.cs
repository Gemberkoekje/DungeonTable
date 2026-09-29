namespace DungeonTable.Core.Dossier;

/// <summary>
/// The fourth dossier tier, beside the per-area, per-floor and shared-rules ones: the campaign-wide
/// background that belongs to no single level. The place's history, the thing that makes it
/// strange, the way in the party takes, and the dungeon-wide level table.
/// </summary>
/// <remarks>
/// This is the answer to "what <em>is</em> this place?", which players ask on night one. It also
/// holds the few campaign-wide settings the app would otherwise have to guess: where the DM screen
/// opens, and what the level table is called.
/// </remarks>
public sealed class CampaignDossier
{
    /// <summary>The campaign's display title ("The Silent Bell").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Source page(s) the dossier was taken from ("4-7"), or an empty string.</summary>
    public string Pages { get; init; } = string.Empty;

    /// <summary>
    /// The area the DM screen opens on before anything is clicked
    /// ("data_dossiers_level_1_area_1"). An empty string opens on the first area of the first level
    /// file.
    /// </summary>
    public string StartAreaNodeId { get; init; } = string.Empty;

    /// <summary>The background sections: the place's history, the way in, and the like.</summary>
    public IReadOnlyList<DossierBlock> Sections { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>
    /// The heading over <see cref="Levels"/> ("Levels of the Undercroft"); an empty string reads as
    /// "Levels".
    /// </summary>
    public string LevelsHeading { get; init; } = string.Empty;

    /// <summary>
    /// A short note beside that heading, saying what the table is for ("the crypt stays sealed until
    /// the bell rings"), or an empty string.
    /// </summary>
    public string LevelsNote { get; init; } = string.Empty;

    /// <summary>The dungeon-wide level table, in book order.</summary>
    public IReadOnlyList<LevelSummary> Levels { get; init; } = Array.Empty<LevelSummary>();

    /// <summary>The "Bonus XP Awards" table, for traps and exceptional roleplaying with key NPCs.</summary>
    public IReadOnlyList<DossierBlock> BonusXp { get; init; } = Array.Empty<DossierBlock>();
}
