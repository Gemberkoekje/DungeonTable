namespace DungeonTable.Core.Stats;

/// <summary>
/// One named entry under a stat block section (a trait, action, bonus action, reaction, or
/// legendary action). <see cref="Text"/> always holds the full original sentence, regardless of
/// how well <see cref="Attacks"/> could be parsed from it — the tab degrades to plain readable
/// text instead of silently dropping a mechanic when the parser can't make sense of a line.
/// </summary>
public sealed class StatBlockEntry
{
    /// <summary>The entry's name ("Multiattack", "Surprise Attack").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The full original text of the entry.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Best-effort structured parse of any attacks named in <see cref="Text"/>; may be empty.</summary>
    public IReadOnlyList<AttackLine> Attacks { get; init; } = Array.Empty<AttackLine>();
}
