namespace DungeonTable.Core.Battle;

/// <summary>
/// The fifteen standard 5e conditions a combatant can be under, toggled from the initiative row.
/// Concentration is deliberately <em>not</em> in here: it is not a condition in the rules, it has
/// its own note text, and it is tracked by <see cref="Combatant.Concentrating"/> instead.
/// </summary>
public enum ConditionKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>Blinded.</summary>
    Blinded = 1,

    /// <summary>Charmed.</summary>
    Charmed = 2,

    /// <summary>Deafened.</summary>
    Deafened = 3,

    /// <summary>Frightened.</summary>
    Frightened = 4,

    /// <summary>Grappled.</summary>
    Grappled = 5,

    /// <summary>Incapacitated.</summary>
    Incapacitated = 6,

    /// <summary>Invisible.</summary>
    Invisible = 7,

    /// <summary>Paralyzed.</summary>
    Paralyzed = 8,

    /// <summary>Petrified.</summary>
    Petrified = 9,

    /// <summary>Poisoned.</summary>
    Poisoned = 10,

    /// <summary>Prone.</summary>
    Prone = 11,

    /// <summary>Restrained.</summary>
    Restrained = 12,

    /// <summary>Stunned.</summary>
    Stunned = 13,

    /// <summary>Unconscious.</summary>
    Unconscious = 14,

    /// <summary>Exhaustion (any level; the level itself goes in the combatant's note).</summary>
    Exhaustion = 15,
}
