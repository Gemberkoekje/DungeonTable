namespace DungeonTable.Core.Dossier;

/// <summary>
/// How precisely a quest beat names where it happens. Beats are targeted structurally rather than by
/// prose so a destination on an unauthored level degrades to a muted label today and becomes a live
/// jump the moment that level is authored, with no re-authoring.
/// </summary>
public enum QuestTargetKind
{
    /// <summary>No destination — the beat happens wherever the party is, or nowhere in particular.</summary>
    None = 0,

    /// <summary>Above ground: the village, the chapel.</summary>
    Surface = 1,

    /// <summary>Somewhere on a dungeon level, without a keyed area.</summary>
    Level = 2,

    /// <summary>A specific keyed area on a dungeon level ("level 3, area 21n").</summary>
    Area = 3,

    /// <summary>A named place that is not a numbered level (a smugglers' cove, a sunken fortress).</summary>
    Place = 4,
}
