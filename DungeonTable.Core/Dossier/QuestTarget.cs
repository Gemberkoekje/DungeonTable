namespace DungeonTable.Core.Dossier;

/// <summary>
/// Where a quest beat happens, named structurally rather than in prose. Every front-matter quest
/// destination sits on a level that is not authored yet, so the quest log shows a muted
/// "Level 3 · area 21n — not authored yet" chip until that level lands, and the same authored data
/// then becomes a live jump button.
/// </summary>
/// <remarks>
/// This deliberately does <b>not</b> go through <c>ICrossRefResolver</c>. That scanner is
/// context-free, so once a second level is authored a bare "area 21n" is ambiguous across all 23
/// levels; a beat that carries its own level number never becomes ambiguous.
/// </remarks>
public sealed class QuestTarget
{
    /// <summary>How precisely the destination is named.</summary>
    public QuestTargetKind Kind { get; init; } = QuestTargetKind.None;

    /// <summary>The dungeon level number, or 0 when the target is not on a numbered level.</summary>
    public int Level { get; init; }

    /// <summary>The keyed area as printed on the book's map ("21n", "14c"), or an empty string.</summary>
    public string AreaKey { get; init; } = string.Empty;

    /// <summary>The destination's display name ("The Sealed Crypt", "The chapel").</summary>
    public string PlaceLabel { get; init; } = string.Empty;

    /// <summary>
    /// The area node id the beat's chip jumps to <em>once that level is authored</em> — written
    /// ahead of time in the canonical <c>data_dossiers_level_&lt;n&gt;_area_&lt;key&gt;</c> form, so no
    /// re-authoring is needed when the level lands. Until a dossier exists under this id the chip is
    /// a muted label, which is also the graceful failure if the eventual id turns out different.
    /// Empty means "never a jump".
    /// </summary>
    public string NodeId { get; init; } = string.Empty;
}
