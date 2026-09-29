namespace DungeonTable.Core.Session;

/// <summary>
/// One quest's progress at this table: its status and which of its beats have been ticked.
/// </summary>
/// <remarks>
/// Beats are recorded by id rather than by index, so re-ordering or inserting a beat in
/// <c>quests.json</c> does not silently shift which ones read as done. An id that no longer exists
/// in the authored data is simply never matched — harmless, and it survives a data edit being
/// reverted.
/// </remarks>
public sealed class QuestProgress
{
    /// <summary>The quest this belongs to (<c>Quest.Id</c>).</summary>
    public string QuestId { get; set; } = string.Empty;

    /// <summary>Where the quest stands.</summary>
    public QuestStatus Status { get; set; } = QuestStatus.None;

    /// <summary>The ids of the beats ticked off, in no particular order.</summary>
    public IReadOnlyList<string> CompletedBeatIds { get; set; } = Array.Empty<string>();
}
