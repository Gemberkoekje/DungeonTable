namespace DungeonTable.Core.Stats;

/// <summary>
/// A best-effort structured parse of one attack named in a <see cref="StatBlockEntry"/>'s text
/// ("Morningstar. Melee Weapon Attack: +4 to hit, reach 5 ft., one target. Hit: 11 (2d8 + 2)
/// piercing damage."). Never authoritative on its own — <see cref="StatBlockEntry.Text"/> always
/// carries the full original sentence, so a parse gap never hides a mechanic from the DM.
/// </summary>
public sealed class AttackLine
{
    /// <summary>The attack's name ("Morningstar").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>What kind of attack this is.</summary>
    public AttackKind Kind { get; init; } = AttackKind.None;

    /// <summary>The to-hit bonus.</summary>
    public int ToHit { get; init; }

    /// <summary>Melee reach in feet, or 0 when the attack has no reach (a ranged-only attack).</summary>
    public int Reach { get; init; }

    /// <summary>Normal range in feet, or 0 when the attack has no range (a melee-only attack).</summary>
    public int RangeNormal { get; init; }

    /// <summary>Long range in feet, or 0 when the attack has no long range.</summary>
    public int RangeLong { get; init; }

    /// <summary>Number of targets the attack can hit ("one target" = 1).</summary>
    public int Targets { get; init; }

    /// <summary>The primary damage dice expression ("2d8 + 2").</summary>
    public string DamageDice { get; init; } = string.Empty;

    /// <summary>The average of <see cref="DamageDice"/>, as printed.</summary>
    public int DamageAverage { get; init; }

    /// <summary>The primary damage type ("piercing").</summary>
    public string DamageType { get; init; } = string.Empty;

    /// <summary>Additional damage riders beyond the primary hit ("plus 7 (2d6) poison damage").</summary>
    public IReadOnlyList<DamageRider> ExtraDamage { get; init; } = Array.Empty<DamageRider>();

    /// <summary>A save-or-effect rider that follows the damage ("the target is grappled"), or empty.</summary>
    public string OnHit { get; init; } = string.Empty;
}
