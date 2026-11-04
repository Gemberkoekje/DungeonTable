namespace DungeonTable.Core.Dossier;

/// <summary>
/// One piece of the campaign's prose that the DM can find by its words and pin: a rules section, one
/// of a floor's facts, a room's read-aloud or detail, a quest's hook, a person's hooks. It is read out
/// of the content as it stands (nothing new is authored), and its <see cref="Key"/> stays the same
/// across a reload as long as the block keeps its heading, so a pin outlives an edit of the text.
/// </summary>
public sealed class Passage
{
    /// <summary>
    /// The passage's stable key ("rules/darkness-below", "level/data_dossiers_level_1/factions/grasks-crew"),
    /// built by <see cref="PassageKeys"/>: what a pin and the dock's sheet name it by.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>The passage's own heading ("Darkness Below", "Read-aloud", "Loose Flagstone").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Where it sits, for reading it out of context ("Rules", "Level 1: The Undercroft › Factions",
    /// "Goblin Den (Level 1, Area 2) › Detail").
    /// </summary>
    public string Trail { get; init; } = string.Empty;

    /// <summary>The prose, with its link markup as written.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>True when it is DM-only and must never be read aloud.</summary>
    public bool Secret { get; init; }

    /// <summary>Where it sits in the campaign, for its badge and its rank.</summary>
    public PassageKind Kind { get; init; } = PassageKind.None;

    /// <summary>
    /// The level node id the prose's links are read on (a room's or a floor's own), or an empty string for
    /// prose about no one floor, as the Campaign tab reads it.
    /// </summary>
    public string Floor { get; init; } = string.Empty;

    /// <summary>
    /// What the passage belongs to and can open: a room's area node id, a person's or an entity's id. Empty
    /// for a passage that stands alone, such as a rules section.
    /// </summary>
    public string OwnerId { get; init; } = string.Empty;
}
