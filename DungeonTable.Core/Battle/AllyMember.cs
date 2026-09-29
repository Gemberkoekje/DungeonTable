namespace DungeonTable.Core.Battle;

/// <summary>
/// One recurring ally in the saved roster — a druid's companion, a hired guard, a familiar that
/// shows up across many sessions. Worth remembering between fights, unlike a one-off summon, which
/// is added straight to a battle without ever landing here.
/// </summary>
/// <remarks>Mutable for the in-app <c>AllyEditor</c>, exactly like <see cref="PartyMember"/>.</remarks>
public sealed class AllyMember
{
    /// <summary>The ally's name ("Shadow").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Who it belongs to ("Rurik's companion") — free text, not a foreign key.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// The stat-library node id backing it ("data_statblocks_monsters_bandit"), or an empty string when it has no stat
    /// block. Adding the ally to a fight pre-fills from the block when one resolves.
    /// </summary>
    public string StatBlockNodeId { get; set; } = string.Empty;

    /// <summary>Armour class; 0 when not recorded.</summary>
    public int ArmourClass { get; set; }

    /// <summary>Maximum hit points; 0 when not recorded.</summary>
    public int MaxHp { get; set; }

    /// <summary>Passive Perception; 0 when not recorded.</summary>
    public int PassivePerception { get; set; }

    /// <summary>The ally's initiative modifier, used by the in-app roll.</summary>
    public int InitiativeModifier { get; set; }
}
