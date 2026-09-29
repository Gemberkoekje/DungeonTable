using System.Globalization;
using System.Text.RegularExpressions;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The form area ids are written in, <c>data_dossiers_level_&lt;n&gt;_area_&lt;key&gt;</c>, by the sample
/// pack and by every campaign so far. The app never builds an id this way. It matters only for a
/// reference written ahead of its floor, a quest beat's destination or a region linked before its
/// level file exists, which has nothing else to be checked against until that floor is written.
/// </summary>
internal static partial class AreaIds
{
    /// <summary>The id the area printed under a key on a floor is written with.</summary>
    /// <param name="level">The floor's number.</param>
    /// <param name="key">The area's printed key ("21n").</param>
    /// <returns>The id ("data_dossiers_level_3_area_21n").</returns>
    internal static string For(int level, string key) =>
        $"data_dossiers_level_{level.ToString(CultureInfo.InvariantCulture)}_area_{key}";

    /// <summary>The floor an id written in this form belongs to.</summary>
    /// <param name="id">The id.</param>
    /// <param name="level">The floor's number, when it is in this form.</param>
    /// <returns>True when it is in this form.</returns>
    internal static bool TryFloor(string id, out int level)
    {
        Match match = Form().Match(id ?? string.Empty);
        level = 0;
        return match.Success
            && int.TryParse(match.Groups["level"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out level)
            && level > 0;
    }

    [GeneratedRegex(@"^data_dossiers_level_(?<level>[0-9]{1,4})_area_[0-9]{1,4}[a-z]?$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Form();
}
