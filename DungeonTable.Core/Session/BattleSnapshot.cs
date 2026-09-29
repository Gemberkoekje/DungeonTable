using DungeonTable.Core.Battle;

namespace DungeonTable.Core.Session;

/// <summary>
/// The persisted fight: the initiative order, every combatant, whose turn it is, and the counters
/// the fight hands out ids and ordinals from.
/// </summary>
/// <remarks>
/// <para>
/// The order and the combatants are stored as the live <see cref="InitiativeEntry"/> and
/// <see cref="Combatant"/> types rather than parallel DTOs. That is a considered trade: a hand-copied
/// DTO drifts silently — the day a combatant gains a field (death saves, innate spell uses) the field
/// is simply not saved, and nothing fails. Reusing the types means new state persists for free, and
/// <c>Restored_combatant_state_survives_a_json_round_trip</c> walks the type by reflection so a
/// property that <em>cannot</em> round-trip fails the build instead.
/// </para>
/// <para>
/// The three counters are the reason this type exists at all. <c>BattleState</c> issues ids from a
/// running number and ordinals ("Bugbear 3") from a per-type tally, and neither is on the render
/// snapshot the Battle tab reads. Restoring the combatants without them would restart both counters
/// at zero, so the next creature added after a restart would collide with an id already in the fight
/// and the next bugbear would be a second "Bugbear 1".
/// </para>
/// </remarks>
public sealed class BattleSnapshot
{
    /// <summary>The fight's id; an empty string when no fight is running.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The node id of the area the fight started in.</summary>
    public string AreaNodeId { get; set; } = string.Empty;

    /// <summary>The area's display title at the time the fight started.</summary>
    public string AreaTitle { get; set; } = string.Empty;

    /// <summary>The current round, counting from 1.</summary>
    public int Round { get; set; }

    /// <summary>Index into <see cref="Entries"/> of whose turn it is.</summary>
    public int TurnIndex { get; set; }

    /// <summary>True once the DM has started running turns.</summary>
    public bool Started { get; set; }

    /// <summary>The last id number issued, so restored ids are never handed out twice.</summary>
    public int LastIdNumber { get; set; }

    /// <summary>The next insertion sequence, the initiative sort's final tie-break.</summary>
    public int NextSequence { get; set; }

    /// <summary>How many of each creature type have been numbered, so "Bugbear 3" follows 1 and 2.</summary>
    public IReadOnlyList<IssuedOrdinal> IssuedOrdinals { get; set; } = Array.Empty<IssuedOrdinal>();

    /// <summary>The initiative order, in display order.</summary>
    public IReadOnlyList<InitiativeEntry> Entries { get; set; } = Array.Empty<InitiativeEntry>();

    /// <summary>Every combatant in the fight, including members inside a collapsed group row.</summary>
    public IReadOnlyList<Combatant> Combatants { get; set; } = Array.Empty<Combatant>();

    /// <summary>True when there is a fight to restore at all.</summary>
    public bool HasBattle() => Id.Length > 0;
}
