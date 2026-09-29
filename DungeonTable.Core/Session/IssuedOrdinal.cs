namespace DungeonTable.Core.Session;

/// <summary>
/// How many combatants of one creature type the running fight has numbered — the "3" behind the next
/// creature being "Bugbear 4". Stored as a list of pairs rather than a dictionary so the document has
/// one obvious shape whatever the key looks like (a stat-block node id, or a <c>name:</c> key for an
/// unstatted creature).
/// </summary>
public sealed class IssuedOrdinal
{
    /// <summary>The type key ordinals are counted per: a stat-block node id, or <c>name:bugbear</c>.</summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>The highest ordinal issued for that type in this fight.</summary>
    public int Issued { get; set; }
}
