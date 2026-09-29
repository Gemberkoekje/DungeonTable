using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Battle;

namespace DungeonTable.Web.Services;

/// <summary>
/// The conditions the DM can put on a combatant, in the order the rules list them, with the label
/// each chip shows. Derived from <see cref="ConditionKind"/> itself rather than hand-listed, so the
/// initiative row and the combatant detail pane cannot drift apart or miss a condition the enum
/// gains later.
/// </summary>
public static class ConditionPalette
{
    /// <summary>Every real condition, in rules order; <see cref="ConditionKind.None"/> is excluded.</summary>
    public static readonly IReadOnlyList<ConditionKind> All =
        Enum.GetValues<ConditionKind>().Where(condition => condition != ConditionKind.None).ToArray();

    /// <summary>The chip label for a condition ("prone").</summary>
    /// <param name="condition">The condition.</param>
    /// <returns>Its lower-cased name.</returns>
    public static string Label(ConditionKind condition) => condition.ToString().ToLowerInvariant();
}
