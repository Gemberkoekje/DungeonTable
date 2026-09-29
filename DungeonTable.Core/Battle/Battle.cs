namespace DungeonTable.Core.Battle;

/// <summary>
/// An immutable snapshot of the one live fight: where it started, how far it has got, and every
/// combatant in it. Handed out by <c>BattleState</c> so the Battle tab renders from a consistent
/// picture rather than reading a structure that is being mutated underneath it.
/// </summary>
public sealed class Battle
{
    /// <summary>Stable id of this fight; empty when no battle is running.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The node id of the area the fight started in, for the "back to briefing" link.</summary>
    public string AreaNodeId { get; init; } = string.Empty;

    /// <summary>The area's display title at the time the fight started ("Goblin Den").</summary>
    public string AreaTitle { get; init; } = string.Empty;

    /// <summary>The current round, counting from 1.</summary>
    public int Round { get; init; }

    /// <summary>Index into <see cref="Entries"/> of whose turn it is.</summary>
    public int TurnIndex { get; init; }

    /// <summary>True once the DM has started running turns (as opposed to still setting the fight up).</summary>
    public bool Started { get; init; }

    /// <summary>The initiative order, in display order.</summary>
    public IReadOnlyList<InitiativeEntry> Entries { get; init; } = Array.Empty<InitiativeEntry>();

    /// <summary>
    /// Every combatant in the fight, including the members hidden inside a collapsed group row.
    /// Look one up by the ids on <see cref="InitiativeEntry.MemberIds"/>.
    /// </summary>
    public IReadOnlyList<Combatant> Combatants { get; init; } = Array.Empty<Combatant>();

    /// <summary>True when there is a fight to show at all.</summary>
    public bool IsActive => Id.Length > 0;
}
