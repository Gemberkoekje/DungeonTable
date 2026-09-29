using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Stats;

namespace DungeonTable.Web.Services;

/// <summary>
/// Small arithmetic helpers over a <see cref="StatBlock"/> so the DM never does 5e modifier math
/// mid-fight: ability modifiers, the printed-or-derived bonus for all six saving throws (a
/// proficient save uses its printed bonus; a non-proficient one derives from the ability score), and
/// how many spell slots of a level a combatant has left.
/// </summary>
public static class StatBlockMath
{
    /// <summary>The six ability short codes in stat-block order.</summary>
    public static readonly IReadOnlyList<string> AbilityCodes = new[] { "Str", "Dex", "Con", "Int", "Wis", "Cha" };

    /// <summary>Computes the 5e ability modifier for a score.</summary>
    /// <param name="score">The ability score (1-30).</param>
    /// <returns>The modifier, from -5 (score 1) upward.</returns>
    public static int AbilityModifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    /// <summary>Formats a modifier or bonus with an explicit sign ("+2", "-1", "+0").</summary>
    /// <param name="value">The modifier or bonus.</param>
    /// <returns>The signed string.</returns>
    public static string FormatModifier(int value) => value >= 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Reads the ability score named by a short code ("Str", "Dex", ...) from a score block.</summary>
    /// <param name="abilities">The ability scores.</param>
    /// <param name="code">A short ability code, case-insensitive.</param>
    /// <returns>The score, or 10 (a +0 modifier) when the code is unrecognised.</returns>
    public static int ScoreFor(AbilityScores abilities, string code) => code.ToUpperInvariant() switch
    {
        "STR" => abilities.Str,
        "DEX" => abilities.Dex,
        "CON" => abilities.Con,
        "INT" => abilities.Intelligence,
        "WIS" => abilities.Wis,
        "CHA" => abilities.Cha,
        _ => 10,
    };

    /// <summary>
    /// The bonus for one of the six saving throws: the printed proficient bonus when the block lists
    /// one for this ability, otherwise the plain ability modifier.
    /// </summary>
    /// <param name="block">The stat block.</param>
    /// <param name="code">A short ability code ("Str", "Dex", ...), case-insensitive.</param>
    /// <returns>The save bonus to show.</returns>
    public static int SaveFor(StatBlock block, string code)
    {
        SaveBonus proficient = block.SavingThrows.FirstOrDefault(
            save => string.Equals(save.Ability, code, StringComparison.OrdinalIgnoreCase));

        return proficient is not null ? proficient.Bonus : AbilityModifier(ScoreFor(block.Abilities, code));
    }

    /// <summary>True when the block lists a printed proficient bonus for this ability's save.</summary>
    /// <param name="block">The stat block.</param>
    /// <param name="code">A short ability code ("Str", "Dex", ...), case-insensitive.</param>
    /// <returns><c>true</c> when the save is proficient.</returns>
    public static bool IsProficientSave(StatBlock block, string code) =>
        block.SavingThrows.Any(save => string.Equals(save.Ability, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// How many spell slots of one level a combatant has burned this fight. The battle only ever
    /// records levels that have been spent, so an untouched level is simply absent.
    /// </summary>
    /// <param name="spent">The combatant's spent-slot tally.</param>
    /// <param name="level">The spell level (1-9).</param>
    /// <returns>The number spent, or 0.</returns>
    public static int SlotsSpent(IReadOnlyList<SpentSlot> spent, int level)
    {
        SpentSlot found = spent.FirstOrDefault(slot => slot.Level == level);
        return found is not null ? found.Spent : 0;
    }
}
