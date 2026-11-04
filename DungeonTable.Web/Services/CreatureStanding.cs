using System.Collections.Generic;
using System.Linq;

namespace DungeonTable.Web.Services;

/// <summary>
/// What is left of one creature an area lists, now: the listing read against what the table recorded
/// here and, for one somebody or one band, in every other room that lists them.
/// </summary>
/// <remarks>
/// <para>
/// A stat block listed as itself is a kind of creature: the bugbears of one room are not the bugbears
/// of the next, so only this room's record counts. One somebody (an entity, an NPC, a character) is
/// the same somebody in every room that lists him: Grask dealt with in the workshop is dealt with on
/// the stair as well. Fled is the exception, and why it is recorded apart from dealt with: Grask
/// fleeing the workshop is gone from it and still up on the stair.
/// </para>
/// <para>
/// A band (an entity with a count, such as a gang's seven bandits) is the same for its members: each
/// room lists some of the band, a published adventure often lists them more than once (a hall that
/// lists the four in the barracks and the two at the gate, and then all of them again where they
/// gather), and a room never shows more of them than the band has left. Two dealt with at the gate
/// leave none there, the barracks' four, and five of seven in the hall where they gather.
/// </para>
/// <para>
/// What was dealt with is counted from every room's record, including a room whose listing has since
/// been edited away: the fight still happened. Nothing here is stored; it is worked out at each render
/// from <see cref="CampaignState"/>, so it can never go stale.
/// </para>
/// </remarks>
public sealed class CreatureStanding
{
    /// <summary>How many the area lists; zero for a count the DM rolls.</summary>
    public int Listed { get; init; }

    /// <summary>The dice the area lists instead of a number ("1d4+1"), or an empty string.</summary>
    public string Expression { get; init; } = string.Empty;

    /// <summary>True when the area lists a count the DM rolls, which cannot be counted down one at a time.</summary>
    public bool Rolled => Expression.Length > 0;

    /// <summary>How many were dealt with in this room.</summary>
    public int DealtHere { get; init; }

    /// <summary>How many fled from this room.</summary>
    public int FledHere { get; init; }

    /// <summary>
    /// How many are still here. A rolled count still here reads as its pre-fill, since the dice are not
    /// rolled yet.
    /// </summary>
    public int Left { get; init; }

    /// <summary>True when none of them is here any more: dealt with, fled, or dealt with elsewhere.</summary>
    public bool Gone => Left == 0;

    /// <summary>
    /// True when this room's own record would leave some of them here, but they were dealt with in
    /// another room: one somebody, or the whole band.
    /// </summary>
    public bool GoneElsewhere { get; init; }

    /// <summary>The other areas where some of them were dealt with, by node id.</summary>
    public IReadOnlyList<string> DealtIn { get; init; } = Array.Empty<string>();

    /// <summary>True for one somebody or one band, whose record is shared between rooms.</summary>
    public bool Shared { get; init; }

    /// <summary>How many there are in all, for one somebody (one) or a band; zero for a kind of creature.</summary>
    public int BandSize { get; init; }

    /// <summary>How many of them are still up, across every room; zero for a kind of creature.</summary>
    public int BandLeft { get; init; }

    /// <summary>Works out what is left of one creature an area lists.</summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="row">The creature, as the encounter strip lists it.</param>
    /// <param name="campaign">The table's record.</param>
    /// <returns>Its standing now.</returns>
    public static CreatureStanding Of(string areaNodeId, EncounterMonster row, CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(campaign);

        CreatureRecord here = campaign.RecordOf(areaNodeId, row.NodeId);
        bool rolled = row.CountExpression.Length > 0;
        int listed = rolled ? 0 : Math.Max(row.SuggestedCount, 1);

        // A rolled count has no number to count down from: anything recorded here takes all of it, and
        // while nothing is, what is left reads as its pre-fill.
        int hereLeft = Math.Max(0, listed - here.Dealt - here.Fled);
        if (rolled)
        {
            hereLeft = Math.Max(row.SuggestedCount, 1);
        }

        if (here.Whole || (rolled && here.Any))
        {
            hereLeft = 0;
        }

        int dealtHere = here.Whole ? listed : Math.Min(here.Dealt, listed);

        if (!row.Shared)
        {
            return new CreatureStanding
            {
                Listed = listed,
                Expression = row.CountExpression,
                DealtHere = dealtHere,
                FledHere = here.Fled,
                Left = hereLeft,
            };
        }

        // A band is as large as it says; one somebody is one. A listing of more than that (content the
        // checks flag) widens it rather than hiding creatures the room says are there.
        int band = Math.Max(Math.Max(row.BandSize, 1), listed);
        IReadOnlyList<KeyValuePair<string, int>> dealt = campaign.DealtByArea(row.NodeId);
        int bandLeft = Math.Max(0, band - dealt.Sum(area => area.Value));

        // A rolled count is not capped by the band (its number is not known), only emptied with it.
        int left = rolled ? hereLeft : Math.Min(hereLeft, bandLeft);
        if (bandLeft == 0)
        {
            left = 0;
        }

        return new CreatureStanding
        {
            Listed = listed,
            Expression = row.CountExpression,
            DealtHere = dealtHere,
            FledHere = here.Fled,
            Left = left,
            GoneElsewhere = left == 0 && hereLeft > 0,
            DealtIn = dealt
                .Select(area => area.Key)
                .Where(area => !string.Equals(area, areaNodeId, StringComparison.Ordinal))
                .OrderBy(area => area, StringComparer.Ordinal)
                .ToArray(),
            Shared = true,
            BandSize = band,
            BandLeft = bandLeft,
        };
    }

    /// <summary>
    /// The rows of an area that are still up, each carrying what is left of it as its count, as the
    /// badges, the peek card and the Nearby list show them.
    /// </summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="rows">The creatures the area lists.</param>
    /// <param name="campaign">The table's record.</param>
    /// <returns>The rows still up, in the area's order.</returns>
    public static IReadOnlyList<EncounterMonster> StillUp(string areaNodeId, IEnumerable<EncounterMonster> rows, CampaignState campaign)
    {
        var up = new List<EncounterMonster>();
        foreach (EncounterMonster row in (rows ?? Array.Empty<EncounterMonster>()).Where(row => row is not null))
        {
            CreatureStanding standing = Of(areaNodeId, row, campaign);
            if (standing.Gone)
            {
                continue;
            }

            up.Add(standing.Rolled || standing.Left == standing.Listed ? row : row.WithCount(standing.Left));
        }

        return up;
    }
}
