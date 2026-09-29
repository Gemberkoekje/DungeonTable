namespace DungeonTable.Core.Stats;

/// <summary>
/// A creature's spellcasting trait: the caster stats, its slots, and the spells it knows or
/// prepares. A non-caster's <see cref="StatBlock"/> carries an empty instance (<see cref="Ability"/>
/// blank, <see cref="Spells"/> empty) rather than a nullable reference.
/// </summary>
public sealed class SpellcastingBlock
{
    /// <summary>The spellcasting ability ("Intelligence"), or empty when the creature does not cast spells.</summary>
    public string Ability { get; init; } = string.Empty;

    /// <summary>Spell save DC.</summary>
    public int SaveDc { get; init; }

    /// <summary>Spell attack bonus.</summary>
    public int AttackBonus { get; init; }

    /// <summary>Caster level, or 0 when the creature casts innately rather than as a leveled caster.</summary>
    public int CasterLevel { get; init; }

    /// <summary>Spell slots by level, empty for an innate/at-will caster.</summary>
    public IReadOnlyList<SpellSlotLevel> Slots { get; init; } = Array.Empty<SpellSlotLevel>();

    /// <summary>The spells known or prepared.</summary>
    public IReadOnlyList<SpellReference> Spells { get; init; } = Array.Empty<SpellReference>();

    /// <summary>A qualifier on how the creature casts ("innate", "psionics (Charisma)"), or empty.</summary>
    public string Note { get; init; } = string.Empty;
}
