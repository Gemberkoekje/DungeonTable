namespace DungeonTable.Web.Services;

/// <summary>How an ability check is written on a pill or a line: "Perception DC 12".</summary>
public static class SkillCheckText
{
    /// <summary>The check as a pill shows it: the most specific part of its ability, then its DC.</summary>
    /// <param name="check">The check.</param>
    /// <returns>"Perception DC 12", or "Check DC 12" for a check that names no ability.</returns>
    public static string Pill(Core.Dossier.SkillCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return Skill(check.Ability) + " DC " + check.Dc.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The most specific part of an ability: the skill in parentheses when there is one ("Wisdom
    /// (Perception)" is "Perception"), otherwise the ability as written.
    /// </summary>
    /// <param name="ability">The ability, as the dossier writes it.</param>
    /// <returns>The part to show; "Check" when there is none.</returns>
    public static string Skill(string ability)
    {
        if (string.IsNullOrWhiteSpace(ability))
        {
            return "Check";
        }

        int open = ability.IndexOf('(', StringComparison.Ordinal);
        int close = ability.IndexOf(')', StringComparison.Ordinal);
        if (open >= 0 && close > open + 1)
        {
            return ability.Substring(open + 1, close - open - 1);
        }

        return ability;
    }
}
