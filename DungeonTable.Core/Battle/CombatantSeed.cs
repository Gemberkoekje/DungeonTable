namespace DungeonTable.Core.Battle;

/// <summary>
/// The starting numbers for a combatant being added to a fight, gathered in one object rather than
/// a long parameter list. Everything except <see cref="Name"/> is optional: a combatant with no
/// stat block, no HP and no AC still joins the initiative order, because a missing extraction (or a
/// creature the DM invented at the table) must never block a fight.
/// </summary>
public sealed class CombatantSeed
{
    /// <summary>What is being added (player / monster / ally).</summary>
    public CombatantKind Kind { get; init; } = CombatantKind.None;

    /// <summary>The display name, or the group name that members are numbered from ("Bugbear").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The stat-library node id, or an empty string when there is no stat block.</summary>
    public string StatBlockNodeId { get; init; } = string.Empty;

    /// <summary>Maximum (and starting) hit points; 0 when unknown.</summary>
    public int MaxHp { get; init; }

    /// <summary>Armour class; 0 when unknown.</summary>
    public int ArmourClass { get; init; }

    /// <summary>Passive Perception; 0 when unknown.</summary>
    public int PassivePerception { get; init; }

    /// <summary>The initiative modifier the in-app roll adds to a d20.</summary>
    public int InitiativeModifier { get; init; }
}
