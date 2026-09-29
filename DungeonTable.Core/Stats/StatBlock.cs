namespace DungeonTable.Core.Stats;

/// <summary>
/// A full monster/NPC stat block, extracted from the rulebooks and keyed by the node id links and
/// creature lists resolve to ("data_statblocks_monsters_bugbear"), so a link in prose, an area's
/// creature, and a battle combatant all resolve to the same block.
/// </summary>
public sealed class StatBlock
{
    /// <summary>The node id this block is keyed to ("data_statblocks_monsters_bugbear").</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>The creature's name ("Bugbear").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Size category ("Medium").</summary>
    public string Size { get; init; } = string.Empty;

    /// <summary>Creature type, including any parenthetical tag ("humanoid (goblinoid)").</summary>
    public string CreatureType { get; init; } = string.Empty;

    /// <summary>Alignment as printed ("chaotic evil").</summary>
    public string Alignment { get; init; } = string.Empty;

    /// <summary>Armour class.</summary>
    public int ArmourClass { get; init; }

    /// <summary>The AC source note ("hide armor, shield"), or empty when none is printed.</summary>
    public string ArmourNote { get; init; } = string.Empty;

    /// <summary>Average hit points, as printed.</summary>
    public int AverageHitPoints { get; init; }

    /// <summary>Hit dice expression ("5d8 + 5").</summary>
    public string HitDice { get; init; } = string.Empty;

    /// <summary>The creature's movement modes.</summary>
    public IReadOnlyList<Speed> Speeds { get; init; } = Array.Empty<Speed>();

    /// <summary>The six ability scores.</summary>
    public AbilityScores Abilities { get; init; } = new();

    /// <summary>Proficient saving throws only; the rest derive from <see cref="Abilities"/>.</summary>
    public IReadOnlyList<SaveBonus> SavingThrows { get; init; } = Array.Empty<SaveBonus>();

    /// <summary>Proficient skills.</summary>
    public IReadOnlyList<SkillBonus> Skills { get; init; } = Array.Empty<SkillBonus>();

    /// <summary>Damage vulnerabilities, as printed.</summary>
    public IReadOnlyList<string> DamageVulnerabilities { get; init; } = Array.Empty<string>();

    /// <summary>Damage resistances, as printed.</summary>
    public IReadOnlyList<string> DamageResistances { get; init; } = Array.Empty<string>();

    /// <summary>Damage immunities, as printed.</summary>
    public IReadOnlyList<string> DamageImmunities { get; init; } = Array.Empty<string>();

    /// <summary>Condition immunities, as printed.</summary>
    public IReadOnlyList<string> ConditionImmunities { get; init; } = Array.Empty<string>();

    /// <summary>Senses other than passive Perception ("darkvision 60 ft.").</summary>
    public IReadOnlyList<string> Senses { get; init; } = Array.Empty<string>();

    /// <summary>Passive Perception score.</summary>
    public int PassivePerception { get; init; }

    /// <summary>Languages known, as printed ("Common, Goblin"), one entry per language ("-" omitted, empty list instead).</summary>
    public IReadOnlyList<string> Languages { get; init; } = Array.Empty<string>();

    /// <summary>Challenge rating as printed ("1/2", "13").</summary>
    public string ChallengeRating { get; init; } = string.Empty;

    /// <summary>Experience points awarded.</summary>
    public int Xp { get; init; }

    /// <summary>Proficiency bonus implied by the challenge rating.</summary>
    public int ProficiencyBonus { get; init; }

    /// <summary>Passive traits (before the Actions header).</summary>
    public IReadOnlyList<StatBlockEntry> Traits { get; init; } = Array.Empty<StatBlockEntry>();

    /// <summary>Entries under the Actions header, in book order (Multiattack first, where present).</summary>
    public IReadOnlyList<StatBlockEntry> Actions { get; init; } = Array.Empty<StatBlockEntry>();

    /// <summary>Entries under the Bonus Actions header.</summary>
    public IReadOnlyList<StatBlockEntry> BonusActions { get; init; } = Array.Empty<StatBlockEntry>();

    /// <summary>Entries under the Reactions header.</summary>
    public IReadOnlyList<StatBlockEntry> Reactions { get; init; } = Array.Empty<StatBlockEntry>();

    /// <summary>Entries under the Legendary Actions header.</summary>
    public IReadOnlyList<StatBlockEntry> LegendaryActions { get; init; } = Array.Empty<StatBlockEntry>();

    /// <summary>How many legendary actions the creature can take per round; 0 when it has none.</summary>
    public int LegendaryActionsPerRound { get; init; }

    /// <summary>The creature's spellcasting trait; an empty instance when it does not cast spells.</summary>
    public SpellcastingBlock Spellcasting { get; init; } = new();

    /// <summary>The source book this block was extracted from ("SRD_CC_v5.1.pdf").</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>The location within <see cref="Source"/> ("p.266").</summary>
    public string SourceLocation { get; init; } = string.Empty;
}
