using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Session;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Web.Services;

/// <summary>
/// Works out what an area's fight is made of, so "add to battle" starts from the book rather than
/// from the DM's memory.
/// </summary>
/// <remarks>
/// <para>
/// An area's creature list is taken at its word: the list is the encounter, with the authored counts.
/// Nothing is read out of its prose, because a mention is not
/// an occupant: "disguised as vampires" puts no vampire in the room, and an empty list means nobody is
/// there.
/// </para>
/// <para>
/// Stateless and side-effect-free (the resolver caches per text), so it registers as a singleton.
/// </para>
/// </remarks>
public sealed class EncounterSource
{
    private readonly ICrossRefResolver crossRefs;
    private readonly IContentProjection content;
    private readonly IStatLibrary stats;

    /// <summary>Creates the encounter source over the resolver, the content projection and the stat library.</summary>
    /// <param name="crossRefs">Reads the links in a creature's note, so the strip shows their words.</param>
    /// <param name="content">Supplies the source citations of the stat blocks the creatures use.</param>
    /// <param name="stats">Says which creatures have an extracted stat block to pre-fill from.</param>
    public EncounterSource(ICrossRefResolver crossRefs, IContentProjection content, IStatLibrary stats)
    {
        this.crossRefs = crossRefs;
        this.content = content;
        this.stats = stats;
    }

    /// <summary>
    /// Lists the creatures an area lists, one row per creature in the order they are first listed.
    /// </summary>
    /// <param name="briefing">The area's briefing, for its occupants; may be empty.</param>
    /// <returns>The distinct creatures, deduped by node id (possibly empty).</returns>
    public IReadOnlyList<EncounterMonster> For(RoomBriefing briefing)
    {
        if (briefing is null)
        {
            return Array.Empty<EncounterMonster>();
        }

        // A creature whose reference names nothing is left out: the briefing shows it as broken, and
        // there is nothing to put into a fight.
        return briefing.Occupants
            .Where(occupant => occupant is not null && occupant.NodeId.Length > 0)
            .GroupBy(occupant => occupant.NodeId, StringComparer.Ordinal)
            .Select(listed => Row(listed.ToArray()))
            .ToArray();
    }

    /// <summary>
    /// An area's creatures as the table has them: the ones it lists, and after them, in the order they
    /// came, the ones the DM moved here that it does not list. A creature moved here stays on the list
    /// while some of it is here, or once the table recorded something of it here; one somebody is here
    /// while the last move of him brought him here.
    /// </summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="listed">The creatures the area lists (<see cref="For"/>).</param>
    /// <param name="campaign">The table's record, with the moves.</param>
    /// <returns>The area's rows: the listed ones unchanged, then the ones moved here.</returns>
    public IReadOnlyList<EncounterMonster> WithMoves(string areaNodeId, IReadOnlyList<EncounterMonster> listed, CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        IReadOnlyList<EncounterMonster> rows = listed ?? Array.Empty<EncounterMonster>();
        IReadOnlyList<CreatureMove> into = campaign.MovesInto(areaNodeId);
        if (into.Count == 0)
        {
            return rows;
        }

        var listedIds = new HashSet<string>(rows.Select(row => row.NodeId), StringComparer.Ordinal);
        var all = new List<EncounterMonster>(rows);
        foreach (IGrouping<string, CreatureMove> moved in into.GroupBy(move => move.CreatureId, StringComparer.Ordinal))
        {
            if (listedIds.Contains(moved.Key))
            {
                continue;
            }

            IReadOnlyList<CreatureMove> everyMove = campaign.MovesOf(moved.Key);
            EncounterMonster row = RowFrom(moved.Last().ListedInAreaNodeId, moved.Key);
            int net = moved.Sum(move => move.Count)
                - everyMove.Where(move => string.Equals(move.FromAreaNodeId, areaNodeId, StringComparison.Ordinal)).Sum(move => move.Count);
            bool here = row.Individual
                ? string.Equals(everyMove[^1].ToAreaNodeId, areaNodeId, StringComparison.Ordinal)
                : net > 0;

            if (here || campaign.RecordOf(areaNodeId, moved.Key).Any)
            {
                all.Add(row.Moved(row.Individual ? 1 : moved.Sum(move => move.Count)));
            }
        }

        return all;
    }

    // A creature as the room that lists it lists it. When that room no longer does (its listing was
    // edited since), the creature still shows, under its id, so the DM can see what they moved.
    private EncounterMonster RowFrom(string areaNodeId, string creatureNodeId)
    {
        Result<RoomBriefing> briefing = content.GetBriefing(areaNodeId);
        EncounterMonster found = briefing.IsValid
            ? For(briefing.Value).FirstOrDefault(row => string.Equals(row.NodeId, creatureNodeId, StringComparison.Ordinal))
            : null;
        if (found is not null)
        {
            return found;
        }

        Result<Core.Stats.StatBlock> block = stats.GetMonster(creatureNodeId);
        return new EncounterMonster
        {
            NodeId = creatureNodeId,
            StatBlockNodeId = block.IsValid ? creatureNodeId : string.Empty,
            Name = block.IsValid && block.Value.Name.Length > 0 ? block.Value.Name : creatureNodeId,
            HasStatBlock = block.IsValid,
        };
    }

    // One row for everything listed under one id. Two groups of the same creature in one room ("four
    // at the table", "two by the door") are one row of six, because a row is what gets added and
    // ticked off; their notes are kept side by side.
    private EncounterMonster Row(OccupantEntry[] listed)
    {
        OccupantEntry first = listed[0];
        CountEstimate count = Count(listed);
        int band = listed.Max(occupant => occupant.BandSize);

        string block = first.MonsterRef;
        Result<Core.Stats.StatBlock> found = block.Length > 0
            ? stats.GetMonster(block)
            : Result.WithMessages<Core.Stats.StatBlock>(ValidationMessage.Error("Fights with no stat block.", nameof(block)));
        Result<ReferenceCard> card = block.Length > 0
            ? content.GetReferenceCard(block)
            : Result.WithMessages<ReferenceCard>(ValidationMessage.Error("Fights with no stat block.", nameof(block)));

        bool shared = !string.Equals(first.NodeId, block, StringComparison.Ordinal);
        return new EncounterMonster
        {
            NodeId = first.NodeId,
            StatBlockNodeId = block,
            Name = first.Label,
            SuggestedCount = count.Count,
            CountExpression = count.Expression,
            Context = string.Join(" · ", listed.Select(occupant => Readable(occupant.Note)).Where(note => note.Length > 0)),
            Citation = card.IsValid ? card.Value.Citation : string.Empty,
            HasStatBlock = found.IsValid,

            // A stat block listed as itself is a kind of creature, added as a numbered group. Anyone
            // else (an entity, an NPC, a player character) is one particular somebody, or one band,
            // which is added as a numbered group too.
            Individual = shared && band == 0,
            Shared = shared,
            BandSize = band,

            // Secret only while none of them is known to the players.
            Secret = listed.All(occupant => occupant.Secret),
        };
    }

    // Plain numbers add up. A dice expression cannot be added to anything, so a list that has one
    // takes the first entry's count and leaves the rest to the DM, who reads every note on the row.
    private static CountEstimate Count(OccupantEntry[] listed)
    {
        // A count that does not read (the content checks catch it) still counts as one.
        CountEstimate[] counts = listed
            .Select(occupant => EncounterCount.TryReadAuthored(occupant.Count, out CountEstimate count) ? count : CountEstimate.One)
            .ToArray();

        if (counts.Length == 1 || counts.Any(count => count.Expression.Length > 0))
        {
            return counts[0];
        }

        return new CountEstimate(Math.Min(counts.Sum(count => count.Count), EncounterCount.MaxCount), string.Empty);
    }

    // An author's note as the DM reads it: its links show their words, never their markup.
    private string Readable(string note) =>
        string.IsNullOrWhiteSpace(note) ? string.Empty : CrossRefText.Display(note, crossRefs.Resolve(note)).Trim();
}
