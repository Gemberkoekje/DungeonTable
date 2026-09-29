namespace DungeonTable.Core.Battle;

/// <summary>
/// The saved party roster, persisted to <c>data/party.json</c> and edited in-app (never by hand).
/// Starting a battle seeds one player combatant per member.
/// </summary>
public sealed class Party
{
    /// <summary>The party's members, in the order they are listed and seeded.</summary>
    public IReadOnlyList<PartyMember> Members { get; init; } = Array.Empty<PartyMember>();
}
