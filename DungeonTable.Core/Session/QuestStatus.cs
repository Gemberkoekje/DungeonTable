namespace DungeonTable.Core.Session;

/// <summary>
/// Where a quest stands for this table. Distinct from <c>QuestAvailability</c>, which is a property
/// of the book's text: availability says when a quest <em>can</em> be offered, this says what has
/// actually happened at the table.
/// </summary>
public enum QuestStatus
{
    /// <summary>No progress recorded — the quest has never been touched.</summary>
    None = 0,

    /// <summary>Offerable, but the party has not taken it up.</summary>
    Available = 1,

    /// <summary>The party has accepted it and is working on it.</summary>
    Active = 2,

    /// <summary>Finished.</summary>
    Complete = 3,

    /// <summary>Dropped, failed, or made impossible; kept so the log does not silently forget it.</summary>
    Abandoned = 4,
}
