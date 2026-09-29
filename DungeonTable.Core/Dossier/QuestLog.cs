namespace DungeonTable.Core.Dossier;

/// <summary>
/// The on-disk shape of <c>quests.json</c>: every quest in one document. A container rather than a
/// bare array so a second file (a second adventure's quest set, or a level's side quests) can be
/// dropped in beside it and merged the way <c>level-*.json</c> already is.
/// </summary>
public sealed class QuestLog
{
    /// <summary>The quests, in authored order.</summary>
    public IReadOnlyList<Quest> Quests { get; init; } = Array.Empty<Quest>();
}
