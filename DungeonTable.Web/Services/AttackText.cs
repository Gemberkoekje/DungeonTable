using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Stats;

namespace DungeonTable.Web.Services;

/// <summary>
/// An attack's numbers as short text, the way a DM reads them out mid-fight. The combatant pane and the
/// initiative row's line for an enemy both use these, so the two never word one attack differently.
/// </summary>
public static class AttackText
{
    /// <summary>
    /// How far the attack reaches: "reach 5 ft.", "range 80/320 ft.", or both for a thrown weapon. Empty
    /// when the extractor found neither.
    /// </summary>
    /// <param name="attack">The attack.</param>
    /// <returns>The reach or range, or an empty string.</returns>
    public static string Reach(AttackLine attack)
    {
        if (attack.Reach > 0 && attack.RangeNormal > 0)
        {
            return $"reach {attack.Reach} ft. or range {Range(attack)}";
        }

        if (attack.Reach > 0)
        {
            return $"reach {attack.Reach} ft.";
        }

        return attack.RangeNormal > 0 ? $"range {Range(attack)}" : string.Empty;
    }

    /// <summary>
    /// The damage, average first, because that is the number a DM who is not rolling reads out:
    /// "11 (2d8 + 2) piercing + 7 (2d6) poison". Empty when the extractor found no dice.
    /// </summary>
    /// <param name="attack">The attack.</param>
    /// <returns>The damage, or an empty string.</returns>
    public static string Damage(AttackLine attack)
    {
        var parts = new List<string>();
        if (attack.DamageDice.Length > 0)
        {
            parts.Add($"{attack.DamageAverage} ({attack.DamageDice}) {attack.DamageType}".TrimEnd());
        }

        parts.AddRange(attack.ExtraDamage.Select(rider =>
            $"+ {rider.DamageAverage} ({rider.DamageDice}) {rider.DamageType}".TrimEnd()));

        return string.Join(" ", parts);
    }

    private static string Range(AttackLine attack) =>
        attack.RangeLong > 0 ? $"{attack.RangeNormal}/{attack.RangeLong} ft." : $"{attack.RangeNormal} ft.";
}
