namespace DungeonTable.Web.Services;

/// <summary>
/// How many of a creature an area's creature list calls for. A <em>pre-fill</em> for the encounter
/// strip's stepper and never authoritative: the DM overrides it whenever the table needs something
/// else.
/// </summary>
/// <param name="Count">The number to pre-fill; 1 when the list gives no number.</param>
/// <param name="Expression">
/// The dice expression authored instead of a number ("1d4 + 1"), or an empty string. The stepper
/// stays at 1 and shows the expression: the DM rolls it physically and types the result.
/// </param>
public readonly record struct CountEstimate(int Count, string Expression)
{
    /// <summary>The "no number given" answer: one creature.</summary>
    public static CountEstimate One => new CountEstimate(1, string.Empty);
}
