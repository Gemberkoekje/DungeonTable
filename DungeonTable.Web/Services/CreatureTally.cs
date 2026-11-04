using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DungeonTable.Web.Services;

/// <summary>
/// Says how many creatures a room still holds, and who, in the few characters a badge beside its
/// label or a line under a neighbouring room has room for.
/// </summary>
/// <remarks>
/// A count rolled at the table is shown as written, not as a guess: the goblins' "1d4+2" and Nib make
/// "1d4+3". Plain numbers add up, the dice of one size add up ("1d4" twice is "2d4"), and the
/// constants of every count go into one number at the end.
/// </remarks>
public static partial class CreatureTally
{
    /// <summary>The number of creatures in some encounter rows, as a badge shows it.</summary>
    /// <param name="rows">The rows, as the encounter strip lists them; each counts as many as its count says.</param>
    /// <returns>"3", "1d4+3" or "2d6+1d4"; an empty string when there are no rows.</returns>
    public static string Count(IEnumerable<EncounterMonster> rows)
    {
        EncounterMonster[] listed = (rows ?? Array.Empty<EncounterMonster>()).Where(row => row is not null).ToArray();
        if (listed.Length == 0)
        {
            return string.Empty;
        }

        int constant = 0;

        // Dice of each size, in the order the sizes first appear.
        var dice = new List<KeyValuePair<int, int>>();
        foreach (EncounterMonster row in listed)
        {
            Match roll = Dice().Match(row.CountExpression ?? string.Empty);
            if (!roll.Success)
            {
                constant += Math.Max(row.SuggestedCount, 1);
                continue;
            }

            AddDice(dice, Number(roll.Groups["sides"].Value), Number(roll.Groups["count"].Value));
            if (roll.Groups["bonus"].Success)
            {
                int bonus = Number(roll.Groups["bonus"].Value);
                constant += string.Equals(roll.Groups["sign"].Value, "-", StringComparison.Ordinal) ? -bonus : bonus;
            }
        }

        if (dice.Count == 0)
        {
            return constant.ToString(CultureInfo.InvariantCulture);
        }

        var text = new StringBuilder();
        foreach (KeyValuePair<int, int> die in dice)
        {
            if (text.Length > 0)
            {
                text.Append('+');
            }

            text.Append(die.Value.ToString(CultureInfo.InvariantCulture)).Append('d')
                .Append(die.Key.ToString(CultureInfo.InvariantCulture));
        }

        if (constant != 0)
        {
            text.Append(constant > 0 ? "+" : "-").Append(Math.Abs(constant).ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    /// <summary>Who is in some encounter rows, as one line: "Grask, 2 × Bugbear, Tam Brask".</summary>
    /// <param name="rows">The rows, in the order the area lists them.</param>
    /// <returns>The names, with a count before each that is not one; an empty string when there are no rows.</returns>
    public static string Names(IEnumerable<EncounterMonster> rows) =>
        string.Join(", ", (rows ?? Array.Empty<EncounterMonster>())
            .Where(row => row is not null)
            .Select(Name));

    /// <summary>One row's name, with its count before it when that is not one: "2 × Bugbear", "1d4+2 × Goblin".</summary>
    /// <param name="row">The row.</param>
    /// <returns>The name as a line shows it.</returns>
    public static string Name(EncounterMonster row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.CountExpression.Length > 0)
        {
            return row.CountExpression + " × " + row.Name;
        }

        return row.SuggestedCount > 1
            ? row.SuggestedCount.ToString(CultureInfo.InvariantCulture) + " × " + row.Name
            : row.Name;
    }

    private static void AddDice(List<KeyValuePair<int, int>> dice, int sides, int count)
    {
        int index = dice.FindIndex(die => die.Key == sides);
        if (index < 0)
        {
            dice.Add(new KeyValuePair<int, int>(sides, count));
            return;
        }

        dice[index] = new KeyValuePair<int, int>(sides, dice[index].Value + count);
    }

    private static int Number(string digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

    // The dice expressions an area's creature list may hold (EncounterCount reads the same ones).
    [GeneratedRegex(@"^\s*(?<count>\d{1,2})\s*[dD]\s*(?<sides>\d{1,3})(\s*(?<sign>[+\-])\s*(?<bonus>\d{1,3}))?\s*$", RegexOptions.CultureInvariant, 200)]
    private static partial Regex Dice();
}
