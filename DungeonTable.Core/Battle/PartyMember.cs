namespace DungeonTable.Core.Battle;

/// <summary>
/// One player character in the saved party roster — the set of names, ACs and hit points that
/// seeds every fight, so the DM does not retype the party at the start of each session.
/// </summary>
/// <remarks>
/// Mutable so the in-app <c>PartyEditor</c> can two-way bind straight to it: this is a small
/// hand-edited document, not domain state a service guards. Every field except
/// <see cref="Name"/> is optional — a roster entry with only a name is perfectly usable.
/// </remarks>
public sealed class PartyMember
{
    /// <summary>The character's name ("Rurik").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The player behind the character, or an empty string.</summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>The character's class ("Cleric"), or an empty string.</summary>
    public string Class { get; set; } = string.Empty;

    /// <summary>Character level; 0 when not recorded.</summary>
    public int Level { get; set; }

    /// <summary>Armour class; 0 when not recorded.</summary>
    public int ArmourClass { get; set; }

    /// <summary>Maximum hit points; 0 when not recorded.</summary>
    public int MaxHp { get; set; }

    /// <summary>Passive Perception; 0 when not recorded.</summary>
    public int PassivePerception { get; set; }

    /// <summary>The character's initiative modifier, used by the in-app roll.</summary>
    public int InitiativeModifier { get; set; }
}
