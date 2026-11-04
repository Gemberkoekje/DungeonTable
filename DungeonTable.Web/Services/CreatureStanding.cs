using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Session;

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
/// <para>
/// The DM can move a creature to another room (<see cref="CreatureMove"/>). For a kind of creature or a
/// band, a room then has what it lists, plus what was moved in, less what was moved out, and the rest of
/// the arithmetic is the same. One somebody is in one place: once moved, he is in the room the last move
/// put him in and nowhere else, until he flees from it, which sets him loose again wherever the book
/// lists him.
/// </para>
/// </remarks>
public sealed class CreatureStanding
{
    /// <summary>
    /// How many this room has: what it lists, with what was moved in and out; zero for a count the DM
    /// rolls, and one for one somebody the DM moved.
    /// </summary>
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

    /// <summary>True when none of them is here any more: dealt with, fled, moved away, or dealt with elsewhere.</summary>
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

    /// <summary>How many the DM moved into this room.</summary>
    public int MovedHere { get; init; }

    /// <summary>How many the DM moved out of this room.</summary>
    public int MovedAway { get; init; }

    /// <summary>The area the last move into this room came from, by node id; empty when none did.</summary>
    public string MovedFrom { get; init; } = string.Empty;

    /// <summary>
    /// Where they went, by node id: for one somebody the DM moved, the room he is in now; otherwise the
    /// room the last move out of this one went to. Empty when none was moved away.
    /// </summary>
    public string MovedTo { get; init; } = string.Empty;

    /// <summary>
    /// True when they are gone from here because the DM moved them: one somebody now in another room, or
    /// a listing all moved out with none dealt with or fled here.
    /// </summary>
    public bool MovedElsewhere { get; init; }

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
        IReadOnlyList<CreatureMove> moves = campaign.MovesOf(row.NodeId);
        CreatureMove[] into = moves.Where(move => string.Equals(move.ToAreaNodeId, areaNodeId, StringComparison.Ordinal)).ToArray();
        CreatureMove[] outOf = moves.Where(move => string.Equals(move.FromAreaNodeId, areaNodeId, StringComparison.Ordinal)).ToArray();

        if (row.Individual && moves.Count > 0)
        {
            // Put somewhere by the DM, until he flees from there: then he is loose again, wherever the
            // book lists him, and the moves no longer say where he is.
            CreatureMove last = moves[^1];
            if (campaign.RecordOf(last.ToAreaNodeId, row.NodeId).Fled == 0)
            {
                return Placed(areaNodeId, row, campaign, here, last);
            }

            into = Array.Empty<CreatureMove>();
            outOf = Array.Empty<CreatureMove>();
        }

        int movedIn = into.Sum(move => move.Count);
        int movedOut = outOf.Sum(move => move.Count);
        bool rolled = row.CountExpression.Length > 0;
        int authored = row.MovedHere || rolled ? 0 : Math.Max(row.SuggestedCount, 1);
        int listed = rolled ? 0 : Math.Max(0, authored + movedIn - movedOut);

        // A rolled count has no number to count down from: anything recorded here takes all of it, and
        // while nothing is, what is left reads as its pre-fill, with what was moved in and out.
        int hereLeft = Math.Max(0, listed - here.Dealt - here.Fled);
        if (rolled)
        {
            hereLeft = Math.Max(0, Math.Max(row.SuggestedCount, 1) + movedIn - movedOut);
        }

        if (here.Whole || (rolled && here.Any))
        {
            hereLeft = 0;
        }

        // Gone because they were moved only when nothing else happened to them here: a room where some
        // fell and the rest were moved reads as dealt with, and its move has a chip of its own.
        int dealtHere = here.Whole ? listed : Math.Min(here.Dealt, listed);
        bool movedElsewhere = hereLeft == 0 && movedOut > 0 && !here.Any;
        string movedFrom = into.Length > 0 ? into[^1].FromAreaNodeId : string.Empty;
        string movedTo = outOf.Length > 0 ? outOf[^1].ToAreaNodeId : string.Empty;

        if (!row.Shared)
        {
            return new CreatureStanding
            {
                Listed = listed,
                Expression = row.CountExpression,
                DealtHere = dealtHere,
                FledHere = here.Fled,
                Left = hereLeft,
                MovedHere = movedIn,
                MovedAway = movedOut,
                MovedFrom = movedFrom,
                MovedTo = movedTo,
                MovedElsewhere = movedElsewhere,
            };
        }

        // A band is as large as it says; one somebody is one. A listing of more than that (content the
        // checks flag) widens it rather than hiding creatures the room says are there.
        int band = Math.Max(Math.Max(row.BandSize, 1), authored);
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
            DealtIn = OtherAreas(dealt, areaNodeId),
            Shared = true,
            BandSize = band,
            BandLeft = bandLeft,
            MovedHere = movedIn,
            MovedAway = movedOut,
            MovedFrom = movedFrom,
            MovedTo = movedTo,
            MovedElsewhere = movedElsewhere && bandLeft > 0,
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

            up.Add(standing.Rolled || standing.Left == row.SuggestedCount ? row : row.WithCount(standing.Left));
        }

        return up;
    }

    // One somebody the DM put in a room: there, and only there, while he is up.
    private static CreatureStanding Placed(string areaNodeId, EncounterMonster row, CampaignState campaign, CreatureRecord here, CreatureMove last)
    {
        bool isHere = string.Equals(last.ToAreaNodeId, areaNodeId, StringComparison.Ordinal);
        IReadOnlyList<KeyValuePair<string, int>> dealt = campaign.DealtByArea(row.NodeId);
        bool up = dealt.Sum(area => area.Value) == 0;
        int dealtHere = here.Whole ? 1 : Math.Min(here.Dealt, 1);

        return new CreatureStanding
        {
            Listed = 1,
            DealtHere = dealtHere,
            FledHere = here.Fled,
            Left = up && isHere && !here.Any ? 1 : 0,
            GoneElsewhere = !up && dealtHere == 0,
            DealtIn = OtherAreas(dealt, areaNodeId),
            Shared = true,
            BandSize = 1,
            BandLeft = up ? 1 : 0,
            MovedHere = isHere ? 1 : 0,
            MovedAway = isHere ? 0 : 1,
            MovedFrom = isHere ? last.FromAreaNodeId : string.Empty,
            MovedTo = isHere ? string.Empty : last.ToAreaNodeId,
            MovedElsewhere = up && !isHere,
        };
    }

    private static string[] OtherAreas(IEnumerable<KeyValuePair<string, int>> dealt, string areaNodeId) =>
        dealt
            .Select(area => area.Key)
            .Where(area => !string.Equals(area, areaNodeId, StringComparison.Ordinal))
            .OrderBy(area => area, StringComparer.Ordinal)
            .ToArray();
}
