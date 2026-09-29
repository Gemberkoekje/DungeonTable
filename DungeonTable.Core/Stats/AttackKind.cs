namespace DungeonTable.Core.Stats;

/// <summary>Classifies a parsed attack line, for icon/label choice in the combatant detail pane.</summary>
public enum AttackKind
{
    /// <summary>Unknown / unset — the line could not be parsed as an attack.</summary>
    None = 0,

    /// <summary>A melee weapon attack.</summary>
    MeleeWeapon = 1,

    /// <summary>A ranged weapon attack.</summary>
    RangedWeapon = 2,

    /// <summary>An attack usable as either melee or ranged ("Melee or Ranged Weapon Attack").</summary>
    MeleeOrRanged = 3,

    /// <summary>A spell attack.</summary>
    Spell = 4,
}
