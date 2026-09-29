using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DungeonTable.Web.Services;

/// <summary>
/// Reads how many of a creature an area's creature list calls for: a whole number ("4") or a dice
/// expression the DM rolls at the table ("1d4+1").
/// </summary>
/// <remarks>Every answer is a pre-fill on a DM-editable control.</remarks>
public static partial class EncounterCount
{
    /// <summary>
    /// The most of one creature a count may say, and what the strip's stepper goes up to. More than
    /// this in one add is far likelier to be a typo than a real horde.
    /// </summary>
    public const int MaxCount = 99;

    /// <summary>
    /// Reads a count an author wrote in an area's creature list: a whole number from 1 to 99 ("4"),
    /// or a dice expression the DM rolls at the table ("1d4+1"). Nothing is guessed: the author said
    /// how many.
    /// </summary>
    /// <param name="written">The count as authored; empty means one.</param>
    /// <param name="estimate">
    /// The count to pre-fill. Anything unreadable pre-fills one, so a typo still leaves a usable row.
    /// </param>
    /// <returns><c>true</c> when the count is empty, a number in range, or a dice expression.</returns>
    public static bool TryReadAuthored(string written, out CountEstimate estimate)
    {
        string count = (written ?? string.Empty).Trim();
        if (count.Length == 0)
        {
            estimate = CountEstimate.One;
            return true;
        }

        if (int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            && number >= 1
            && number <= MaxCount)
        {
            estimate = new CountEstimate(number, string.Empty);
            return true;
        }

        if (AuthoredDice().IsMatch(count))
        {
            estimate = new CountEstimate(1, Normalise(count));
            return true;
        }

        estimate = CountEstimate.One;
        return false;
    }

    // Squeeze the whitespace inside a dice expression so "1d4  +  1" reads as "1d4 + 1".
    private static string Normalise(string expression) =>
        WhitespaceRuns().Replace(expression.Trim(), " ");

    [GeneratedRegex(@"^\d{1,2}\s*[dD]\s*\d{1,3}(\s*[+\-]\s*\d{1,3})?$", RegexOptions.CultureInvariant, 200)]
    private static partial Regex AuthoredDice();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 200)]
    private static partial Regex WhitespaceRuns();
}
