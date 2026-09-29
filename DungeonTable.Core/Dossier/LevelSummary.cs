namespace DungeonTable.Core.Dossier;

/// <summary>
/// One row of the campaign's level table: a dungeon level, its name, and the character level it is
/// designed for. <c>LevelDossier.DesignedFor</c> says this in prose for the <em>current</em> floor;
/// this is the dungeon-wide table, which is what a rule that holds the party back from a deeper
/// level until it is strong enough needs to adjudicate.
/// </summary>
public sealed class LevelSummary
{
    /// <summary>The dungeon level number, or 0 for a place that is not a numbered level (a town below).</summary>
    public int Level { get; init; }

    /// <summary>The level's name ("The Sealed Crypt").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The character level it is designed for, as printed ("3rd", "5th-6th").</summary>
    public string CharacterLevel { get; init; } = string.Empty;

    /// <summary>
    /// The numeric character level a prerequisite tests against — the lower bound of
    /// <see cref="CharacterLevel"/> where it is a range.
    /// </summary>
    public int RequiredLevel { get; init; }

    /// <summary>True when the level is authored in this app, so the DM can see what exists today.</summary>
    public bool Authored { get; init; }
}
