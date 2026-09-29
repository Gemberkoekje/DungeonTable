namespace DungeonTable.Core.Dossier;

/// <summary>
/// When a quest becomes offerable, as the book divides them: the ones a quest giver presses on the
/// party in the village before their first descent, and the ones that only surface once a
/// prerequisite is met.
/// </summary>
public enum QuestAvailability
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>Available from the outset, before the party's first descent.</summary>
    Starting = 1,

    /// <summary>Offered only once its prerequisites are satisfied.</summary>
    Future = 2,

    /// <summary>A quest found inside the dungeon rather than in the front matter.</summary>
    Side = 3,
}
