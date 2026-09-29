namespace DungeonTable.Core.Battle;

/// <summary>
/// The saved roster of recurring allies, persisted to <c>data/allies.json</c> and edited in-app.
/// Unlike the party it does not seed a fight automatically — the DM adds the ones that turn up.
/// </summary>
public sealed class AllyRoster
{
    /// <summary>The saved allies, in list order.</summary>
    public IReadOnlyList<AllyMember> Members { get; init; } = Array.Empty<AllyMember>();
}
