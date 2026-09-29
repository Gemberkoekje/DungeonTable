namespace DungeonTable.Core.Dossier;

/// <summary>
/// What kind of gate a quest's prerequisite is, so the quest log can tell met from unmet rather than
/// only printing the book's sentence.
/// </summary>
public enum QuestPrerequisiteKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>Another quest (or any one of several) must be complete.</summary>
    QuestComplete = 1,

    /// <summary>The characters must have reached a given experience level.</summary>
    CharacterLevel = 2,
}
