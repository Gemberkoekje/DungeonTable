using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
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

    // One row for everything listed under one id. Two groups of the same creature in one room ("four
    // at the table", "two by the door") are one row of six, because a row is what gets added and
    // ticked off; their notes are kept side by side.
    private EncounterMonster Row(OccupantEntry[] listed)
    {
        OccupantEntry first = listed[0];
        CountEstimate count = Count(listed);

        string block = first.MonsterRef;
        Result<Core.Stats.StatBlock> found = block.Length > 0
            ? stats.GetMonster(block)
            : Result.WithMessages<Core.Stats.StatBlock>(ValidationMessage.Error("Fights with no stat block.", nameof(block)));
        Result<ReferenceCard> card = block.Length > 0
            ? content.GetReferenceCard(block)
            : Result.WithMessages<ReferenceCard>(ValidationMessage.Error("Fights with no stat block.", nameof(block)));

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
            // else (an entity, an NPC, a player character) is one particular somebody.
            Individual = !string.Equals(first.NodeId, block, StringComparison.Ordinal),

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
