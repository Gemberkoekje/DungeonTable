namespace DungeonTable.Core.Battle;

/// <summary>
/// The party roster as the document store holds it: a <see cref="Battle.Party"/> plus the table
/// identity it is keyed by, so one database can hold more than one table's roster.
/// </summary>
/// <remarks>
/// A wrapper rather than an identity on <see cref="Battle.Party"/> itself: the party is a domain
/// model passed around the battle code and seeded into fights, and it has no business carrying a
/// storage key. Mirrors <see cref="StoredAllies"/>.
/// </remarks>
public sealed class StoredParty
{
    /// <summary>The table this roster belongs to; the document's identity.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The stored roster.</summary>
    public Party Party { get; set; } = new Party();
}
