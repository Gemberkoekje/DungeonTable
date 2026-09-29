namespace DungeonTable.Core.Battle;

/// <summary>
/// The ally roster as the document store holds it: an <see cref="AllyRoster"/> plus the table
/// identity it is keyed by. Mirrors <see cref="StoredParty"/> exactly.
/// </summary>
public sealed class StoredAllies
{
    /// <summary>The table this roster belongs to; the document's identity.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The stored roster.</summary>
    public AllyRoster Roster { get; set; } = new AllyRoster();
}
