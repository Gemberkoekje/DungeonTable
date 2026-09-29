namespace DungeonTable.Core.Dossier;

/// <summary>
/// A gate that must be satisfied before a quest is offered. The book states each one in a single
/// italic line; this carries that line verbatim <em>and</em> the structured form behind it, so the
/// quest log can render the author's own phrasing while still colouring the chip met or unmet.
/// </summary>
public sealed class QuestPrerequisite
{
    /// <summary>What kind of gate this is.</summary>
    public QuestPrerequisiteKind Kind { get; init; } = QuestPrerequisiteKind.None;

    /// <summary>
    /// The quests that satisfy this gate, for <see cref="QuestPrerequisiteKind.QuestComplete"/>:
    /// completing <b>any one</b> of them is enough. A list rather than a single id purely so
    /// "complete the Miller's Son <em>or</em> the Lost Hymnal quest" fits without
    /// nesting prerequisite groups; the common case is one entry.
    /// </summary>
    public IReadOnlyList<string> AnyOfQuestIds { get; init; } = Array.Empty<string>();

    /// <summary>The experience level required, for <see cref="QuestPrerequisiteKind.CharacterLevel"/>.</summary>
    public int CharacterLevel { get; init; }

    /// <summary>
    /// The book's own phrasing of the prerequisite. The UI renders this and never reconstructs
    /// English from the structured fields, which exist only to decide met/unmet.
    /// </summary>
    public string Text { get; init; } = string.Empty;
}
