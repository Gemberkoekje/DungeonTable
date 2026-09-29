using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Web.Services;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// One string in the loaded content: where it came from, which property of which type holds it, and
/// the floor its prose is read on (empty for the campaign-wide documents).
/// </summary>
/// <param name="Where">A readable path to the string ("area data_dossiers_level_1_area_2.Glance[0].Body").</param>
/// <param name="Owner">The type that declares the property.</param>
/// <param name="Property">The property's name.</param>
/// <param name="Text">The string itself.</param>
/// <param name="Floor">The level node id a bare <c>[[area 6d]]</c> in it is read on, or empty.</param>
internal sealed record ContentText(string Where, Type Owner, string Property, string Text, string Floor);

/// <summary>
/// The content rules for authored links: every link resolves, links sit only where the app renders
/// them as links, every creature entry names a creature, every exit leads to an area (or waits for its
/// floor), every count reads, and every entity is usable. They read the content through the app's own loaders, so
/// they check what the app will actually show.
/// </summary>
internal static class LinkAudit
{
    /// <summary>
    /// The fields the app renders through <c>CrossRefProse</c>, and so the only fields a link may go
    /// in. Anywhere else the markup would reach the page as brackets. Keep this in step with the
    /// components: a new prose field rendered through <c>CrossRefProse</c> belongs here too.
    /// </summary>
    internal static readonly HashSet<(Type Owner, string Property)> ProseFields = new()
    {
        (typeof(DossierBlock), nameof(DossierBlock.Body)),
        (typeof(AreaDossier), nameof(AreaDossier.ReadAloud)),
        (typeof(LevelDossier), nameof(LevelDossier.WhatDwellsHere)),
        (typeof(LevelDossier), nameof(LevelDossier.Aftermath)),
        (typeof(NpcRoster), nameof(NpcRoster.Summary)),
        (typeof(NpcDossier), nameof(NpcDossier.Summary)),
        (typeof(NpcDossier), nameof(NpcDossier.Appearance)),
        (typeof(PartyDossier), nameof(PartyDossier.Summary)),
        (typeof(CharacterDossier), nameof(CharacterDossier.DmOnly)),
        (typeof(CharacterDossier), nameof(CharacterDossier.Kit)),
        (typeof(CharacterDossier), nameof(CharacterDossier.Backstory)),
        (typeof(Quest), nameof(Quest.Hook)),
        (typeof(QuestBeat), nameof(QuestBeat.Summary)),
        (typeof(QuestBeat), nameof(QuestBeat.Detail)),
        (typeof(SessionLog), nameof(SessionLog.Summary)),
        (typeof(SessionPlan), nameof(SessionPlan.Summary)),
        (typeof(SessionScene), nameof(SessionScene.Purpose)),
        (typeof(StoryDossier), nameof(StoryDossier.Summary)),
        (typeof(DeckCard), nameof(DeckCard.Body)),
        (typeof(Entity), nameof(Entity.Description)),
        (typeof(AreaCreature), nameof(AreaCreature.Note)),
        (typeof(AreaExit), nameof(AreaExit.Note)),
        (typeof(Relation), nameof(Relation.Note)),
    };

    /// <summary>Every string in every loaded document, with the floor its prose belongs to.</summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <returns>The strings, document by document.</returns>
    internal static IEnumerable<ContentText> Texts(IDossierStore dossiers)
    {
        var found = new List<ContentText>();
        foreach (LevelDossier level in dossiers.AllLevels())
        {
            Walk(level, $"level {level.LevelNodeId}", level.LevelNodeId, found);
        }

        foreach (AreaDossier area in dossiers.AllAreas())
        {
            Result<LevelDossier> level = dossiers.GetLevelForArea(area.AreaNodeId);
            Walk(area, $"area {area.AreaNodeId}", level.IsValid ? level.Value.LevelNodeId : string.Empty, found);
        }

        // The campaign-wide documents belong to no floor, so an area link in them has to name one.
        WalkIfAuthored(dossiers.GetReference(), "reference.json", found);
        WalkIfAuthored(dossiers.GetQuests(), "quests", found);
        WalkIfAuthored(dossiers.GetCampaign(), "campaign.json", found);
        WalkIfAuthored(dossiers.GetParty(), "characters.json", found);
        WalkIfAuthored(dossiers.GetNpcs(), "npcs.json", found);
        WalkIfAuthored(dossiers.GetStory(), "story.json", found);
        WalkIfAuthored(dossiers.GetSessions(), "sessions.json", found);
        WalkIfAuthored(dossiers.GetRelations(), "relations.json", found);
        foreach (CardDeck deck in dossiers.AllDecks())
        {
            Walk(deck, $"deck {deck.Id}", string.Empty, found);
        }

        return found;
    }

    /// <summary>Every link in a prose field that names nothing, one sentence each.</summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <param name="targets">What a link can name in it.</param>
    /// <returns>The broken links (empty when there are none).</returns>
    internal static IReadOnlyList<string> BrokenLinks(IDossierStore dossiers, LinkTargets targets)
    {
        var broken = new List<string>();
        foreach (ContentText text in Texts(dossiers).Where(IsProse))
        {
            foreach (MarkedLink link in LinkMarkup.Parse(text.Text))
            {
                Result<LinkTarget> resolved = targets.Resolve(link.Target, text.Floor);
                if (!resolved.IsValid)
                {
                    broken.Add($"{text.Where}: {string.Join(" ", resolved.Messages.Select(message => message.Message))}");
                }
            }
        }

        return broken;
    }

    /// <summary>
    /// Every block with no heading or no body, in every dossier document. A block is shown as its
    /// heading, and opens to show its body, so an empty one is a blank row or opens onto nothing.
    /// </summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <returns>One sentence per empty heading or body (empty when there are none).</returns>
    internal static IReadOnlyList<string> EmptyBlocks(IDossierStore dossiers) =>
        Texts(dossiers)
            .Where(text => text.Owner == typeof(DossierBlock)
                && (text.Property is nameof(DossierBlock.Heading) or nameof(DossierBlock.Body))
                && string.IsNullOrWhiteSpace(text.Text))
            .Select(text => $"{text.Where} is empty: every block needs a heading and a body.")
            .ToList();

    /// <summary>Every string outside the prose fields that holds a link, one sentence each.</summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <returns>The misplaced links (empty when there are none).</returns>
    internal static IReadOnlyList<string> MisplacedLinks(IDossierStore dossiers) =>
        Texts(dossiers)
            .Where(text => !IsProse(text) && LinkMarkup.Parse(text.Text).Count > 0)
            .Select(text => $"{text.Where} holds a link, but {text.Owner.Name}.{text.Property} is not rendered as prose, so it would show as brackets.")
            .ToList();

    /// <summary>
    /// Every creature entry that names nothing, or names something that cannot stand in a room (a
    /// faction, an item, a spell, an area), one sentence each.
    /// </summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <param name="targets">What a reference can name in it.</param>
    /// <returns>The broken entries (empty when there are none).</returns>
    internal static IReadOnlyList<string> BrokenCreatures(IDossierStore dossiers, LinkTargets targets)
    {
        var broken = new List<string>();
        foreach ((AreaDossier area, string floor) in AreasWithFloors(dossiers))
        {
            for (int i = 0; i < area.Creatures.Count; i++)
            {
                string written = area.Creatures[i]?.Ref ?? string.Empty;
                Result<LinkTarget> resolved = targets.Resolve(written, floor);
                if (!resolved.IsValid)
                {
                    broken.Add($"area {area.AreaNodeId}.Creatures[{i}]: {Messages(resolved)}");
                }
                else if (!resolved.Value.CanBeInARoom)
                {
                    broken.Add($"area {area.AreaNodeId}.Creatures[{i}]: '{written}' names {Described(resolved.Value.Kind)}, not a creature, person or player character.");
                }
            }
        }

        return broken;
    }

    /// <summary>
    /// Every exit that leads nowhere: not an area, or an area no loaded floor has. An exit to a floor
    /// that has no level file yet is pending, not broken, since nothing can check it.
    /// </summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <param name="targets">What a reference can name in it.</param>
    /// <returns>The broken exits (empty when there are none).</returns>
    internal static IReadOnlyList<string> BrokenExits(IDossierStore dossiers, LinkTargets targets)
    {
        var broken = new List<string>();
        foreach ((AreaDossier area, string floor) in AreasWithFloors(dossiers))
        {
            for (int i = 0; i < area.Exits.Count; i++)
            {
                string written = area.Exits[i]?.To ?? string.Empty;
                Result<LinkTarget> resolved = targets.Resolve(written, floor);
                if (!resolved.IsValid)
                {
                    broken.Add($"area {area.AreaNodeId}.Exits[{i}]: {Messages(resolved)}");
                }
                else if (resolved.Value.Kind is not (CrossRefKind.Area or CrossRefKind.Pending))
                {
                    broken.Add($"area {area.AreaNodeId}.Exits[{i}]: '{written}' names {Described(resolved.Value.Kind)}, not an area.");
                }
            }
        }

        return broken;
    }

    /// <summary>Every creature count that is neither a whole number from 1 to 99 nor a dice expression.</summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <returns>The unreadable counts (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadCounts(IDossierStore dossiers) =>
        dossiers.AllAreas()
            .SelectMany(area => area.Creatures.Select((creature, i) => (area, creature, i)))
            .Where(entry => entry.creature is not null && !EncounterCount.TryReadAuthored(entry.creature.Count, out _))
            .Select(entry => $"area {entry.area.AreaNodeId}.Creatures[{entry.i}]: the count '{entry.creature.Count}' is neither a number from 1 to {EncounterCount.MaxCount} nor a dice expression such as 1d4+1.")
            .ToList();

    /// <summary>
    /// Every entity a level declares that a link or a card could not use: an id that is not already a
    /// slug, no kind, no name, or a stat block that names no extracted stat block.
    /// </summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <param name="targets">What a reference can name in it.</param>
    /// <param name="stats">The extracted stat blocks.</param>
    /// <returns>The faults (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadEntities(IDossierStore dossiers, LinkTargets targets, IStatLibrary stats)
    {
        var bad = new List<string>();
        foreach (LevelDossier level in dossiers.AllLevels())
        {
            foreach (Entity entity in level.AllEntities())
            {
                string where = $"level {level.LevelNodeId} entity '{entity.Id}'";
                if (entity.Id.Length == 0 || !string.Equals(LinkTargets.Slug(entity.Id), entity.Id, StringComparison.Ordinal))
                {
                    bad.Add($"{where}: an id is written as a slug (lower case, hyphens), like '{LinkTargets.Slug(entity.Id)}'.");
                }

                if (entity.Kind == EntityKind.None)
                {
                    bad.Add($"{where}: it has no kind (creature, npc, faction or item).");
                }

                if (string.IsNullOrWhiteSpace(entity.Name))
                {
                    bad.Add($"{where}: it has no name to show.");
                }

                if (!string.IsNullOrWhiteSpace(entity.StatBlock)
                    && !IsStatBlock(targets.Resolve(entity.StatBlock, level.LevelNodeId), stats))
                {
                    bad.Add($"{where}: its stat block '{entity.StatBlock}' is not an extracted stat block. An individual uses a block; it never is one, and it never uses another individual.");
                }
            }
        }

        return bad;
    }

    /// <summary>
    /// Every NPC whose <c>statBlock</c> names no extracted stat block. Listed in a room, they would join
    /// a fight with no numbers, and nothing at the table would say why.
    /// </summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <param name="targets">What a reference can name in it.</param>
    /// <param name="stats">The extracted stat blocks.</param>
    /// <returns>The faults (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadNpcBlocks(IDossierStore dossiers, LinkTargets targets, IStatLibrary stats)
    {
        Result<NpcRoster> roster = dossiers.GetNpcs();
        if (!roster.IsValid)
        {
            return Array.Empty<string>();
        }

        return roster.Value.Npcs
            .Where(npc => npc is not null && !string.IsNullOrWhiteSpace(npc.StatBlock))
            .Where(npc => !IsStatBlock(targets.Resolve(npc.StatBlock, string.Empty), stats))
            .Select(npc => $"npcs.json '{npc.Id}': its stat block '{npc.StatBlock}' is not an extracted stat block. An NPC uses a block; it never is one, and it never uses another individual.")
            .ToList();
    }

    /// <summary>
    /// Every relation a card or a briefing could not draw: a kind
    /// that is not one of the sixteen (<c>related</c> is the book index's alone), an end that names
    /// nothing, a stat block or a spell, and a relation from something to itself. NPC contacts count
    /// too: a <c>rel</c> needs a <c>ref</c>, and a <c>ref</c> on a contact or a story's cast row has to
    /// open someone's card.
    /// </summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <param name="targets">What a reference can name in it.</param>
    /// <returns>The faults (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadRelations(IDossierStore dossiers, LinkTargets targets)
    {
        var bad = new List<string>();
        foreach ((Relation relation, string floor, string where) in Relations(dossiers))
        {
            if (!RelationKinds.IsAuthored(relation.Rel))
            {
                bad.Add(string.Equals(relation.Rel, RelationKinds.Related, StringComparison.Ordinal)
                    ? $"{where}: 'related' is for the book index only; pick one of the sixteen kinds."
                    : $"{where}: '{relation.Rel}' is not a relation kind; use one of {string.Join(", ", RelationKinds.Authored)}.");
            }

            Result<LinkTarget> a = targets.Resolve(relation.A, floor);
            Result<LinkTarget> b = targets.Resolve(relation.B, floor);
            string aFault = EndFault(relation.A, a);
            string bFault = EndFault(relation.B, b);
            if (aFault.Length > 0)
            {
                bad.Add($"{where}: its a end {aFault}");
            }

            if (bFault.Length > 0)
            {
                bad.Add($"{where}: its b end {bFault}");
            }

            if (a.IsValid && b.IsValid && a.Value.TargetId.Length > 0
                && string.Equals(a.Value.TargetId, b.Value.TargetId, StringComparison.Ordinal))
            {
                bad.Add($"{where}: both ends are '{relation.A}'.");
            }
        }

        bad.AddRange(BadCastRefs(dossiers, targets));
        return bad;
    }

    /// <summary>
    /// Every book index entry that search or a link could not use: an id that is not a slug, no name
    /// or kind, a negative page, and an id that does not answer to the entry itself. That last one catches an id that reads as an area link
    /// (<c>area-22a</c>) and two books claiming one id; an id the campaign's own content answers to
    /// instead is fine, since the entry then extends it.
    /// </summary>
    /// <param name="books">The book index.</param>
    /// <param name="targets">What a link can name, built with the book index.</param>
    /// <returns>The faults (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadBookEntries(IEnumerable<BookIndex> books, LinkTargets targets)
    {
        var bad = new List<string>();
        foreach (BookIndex book in books)
        {
            foreach (BookEntry entry in book.Entries)
            {
                string where = $"book {book.Book} entry '{entry.Id}'";
                if (entry.Id.Length == 0 || !string.Equals(LinkTargets.Slug(entry.Id), entry.Id, StringComparison.Ordinal))
                {
                    bad.Add($"{where}: an id is written as a slug, like '{LinkTargets.Slug(entry.Id)}'.");
                }

                if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Kind))
                {
                    bad.Add($"{where}: it needs a name and a kind.");
                }

                if (entry.Page < 0)
                {
                    bad.Add($"{where}: its page is {entry.Page}; write 0 when it is not known.");
                }

                Result<LinkTarget> answer = targets.Resolve(entry.Id, string.Empty);
                if (!answer.IsValid)
                {
                    bad.Add($"{where}: its id does not reach it: {Messages(answer)}");
                }
                else if (answer.Value.Kind == CrossRefKind.Book && !string.Equals(answer.Value.TargetId, entry.Id, StringComparison.Ordinal))
                {
                    bad.Add($"{where}: its id reaches '{answer.Value.TargetId}' instead.");
                }
            }
        }

        return bad;
    }

    /// <summary>
    /// Every book index link that cannot be drawn: a kind that is neither <c>related</c> nor one of
    /// the sixteen, or an end that names nothing loaded. A book link may end at a stat block or a
    /// spell, unlike an authored relation.
    /// </summary>
    /// <param name="books">The book index.</param>
    /// <param name="targets">What a link can name, built with the book index.</param>
    /// <returns>The faults (empty when there are none).</returns>
    internal static IReadOnlyList<string> BrokenBookLinks(IEnumerable<BookIndex> books, LinkTargets targets)
    {
        var bad = new List<string>();
        foreach (BookIndex book in books)
        {
            for (int i = 0; i < book.Links.Count; i++)
            {
                Relation link = book.Links[i];
                string where = $"book {book.Book} link {i} ({link.A} -> {link.B})";
                if (!string.Equals(link.Rel, RelationKinds.Related, StringComparison.Ordinal) && !RelationKinds.IsAuthored(link.Rel))
                {
                    bad.Add($"{where}: '{link.Rel}' is not a relation kind.");
                }

                foreach (string end in new[] { link.A, link.B })
                {
                    Result<LinkTarget> resolved = targets.Resolve(end, string.Empty);
                    if (!resolved.IsValid || resolved.Value.TargetId.Length == 0)
                    {
                        bad.Add($"{where}: '{end}' names nothing loaded. {Messages(resolved)}");
                    }
                }
            }
        }

        return bad;
    }

    // Every authored relation with the floor its ends are read on: the level files', the campaign
    // list's, and each NPC contact that names a kind.
    private static IEnumerable<(Relation Relation, string Floor, string Where)> Relations(IDossierStore dossiers)
    {
        foreach (LevelDossier level in dossiers.AllLevels())
        {
            for (int i = 0; i < level.Relations.Count; i++)
            {
                yield return (level.Relations[i], level.LevelNodeId, $"level {level.LevelNodeId}.Relations[{i}]");
            }
        }

        Result<RelationList> campaign = dossiers.GetRelations();
        if (campaign.IsValid)
        {
            for (int i = 0; i < campaign.Value.Relations.Count; i++)
            {
                yield return (campaign.Value.Relations[i], string.Empty, $"relations.json.Relations[{i}]");
            }
        }

        Result<NpcRoster> npcs = dossiers.GetNpcs();
        if (npcs.IsValid)
        {
            foreach (NpcDossier npc in npcs.Value.Npcs)
            {
                for (int i = 0; i < npc.Contacts.Count; i++)
                {
                    CastMember contact = npc.Contacts[i];
                    if (!string.IsNullOrWhiteSpace(contact.Rel) && !string.IsNullOrWhiteSpace(contact.Ref))
                    {
                        yield return (new Relation { A = npc.Id, Rel = contact.Rel, B = contact.Ref }, string.Empty, $"npcs.json {npc.Id}.Contacts[{i}]");
                    }
                }
            }
        }
    }

    // What is wrong with one end, as the rest of a sentence, or an empty string when nothing is.
    // A reference that resolves to a stat block the library has extracted: what an individual uses.
    private static bool IsStatBlock(Result<LinkTarget> resolved, IStatLibrary stats) =>
        resolved.IsValid && stats.HasMonster(resolved.Value.TargetId);

    private static string EndFault(string end, Result<LinkTarget> resolved)
    {
        if (!resolved.IsValid)
        {
            return Messages(resolved);
        }

        if (resolved.Value.TargetId.Length == 0)
        {
            return $"'{end}' is on a floor nobody has authored yet, so it has nothing to point at.";
        }

        return resolved.Value.Rulebook
            ? $"'{end}' names a stat block or a spell: relations are between particular ones. Declare an entity that uses it."
            : string.Empty;
    }

    // A contact's or a cast row's ref has to open a card; a contact's rel needs a ref to point at.
    private static IEnumerable<string> BadCastRefs(IDossierStore dossiers, LinkTargets targets)
    {
        var rows = new List<(CastMember Row, string Where)>();
        Result<NpcRoster> npcs = dossiers.GetNpcs();
        if (npcs.IsValid)
        {
            rows.AddRange(npcs.Value.Npcs.SelectMany(npc => npc.Contacts.Select((contact, i) => (contact, $"npcs.json {npc.Id}.Contacts[{i}]"))));
        }

        Result<StoryLibrary> story = dossiers.GetStory();
        if (story.IsValid)
        {
            rows.AddRange(story.Value.Chapters.SelectMany(chapter => chapter.Cast.Select((member, i) => (member, $"story.json '{chapter.Title}'.Cast[{i}]"))));
        }

        foreach ((CastMember row, string where) in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Ref))
            {
                if (!string.IsNullOrWhiteSpace(row.Rel))
                {
                    yield return $"{where}: it has a rel ('{row.Rel}') but no ref to say who the relation is with.";
                }

                continue;
            }

            string fault = EndFault(row.Ref, targets.Resolve(row.Ref, string.Empty));
            if (fault.Length > 0)
            {
                yield return $"{where}: its ref {fault}";
            }
        }
    }

    private static IEnumerable<(AreaDossier Area, string Floor)> AreasWithFloors(IDossierStore dossiers) =>
        dossiers.AllAreas().Select(area =>
        {
            Result<LevelDossier> level = dossiers.GetLevelForArea(area.AreaNodeId);
            return (area, level.IsValid ? level.Value.LevelNodeId : string.Empty);
        });

    private static string Messages<T>(Result<T> result) =>
        string.Join(" ", result.Messages.Select(message => message.Message));

    private static string Described(CrossRefKind kind) => kind switch
    {
        CrossRefKind.Area => "an area",
        CrossRefKind.Faction => "a faction",
        CrossRefKind.Item => "an item",
        CrossRefKind.Spell => "a spell",
        CrossRefKind.Monster => "a creature",
        CrossRefKind.Npc => "a person",
        CrossRefKind.Character => "a player character",
        CrossRefKind.Pending => "an area on a floor that is not authored yet",
        CrossRefKind.Book => "a book index entry",
        _ => "something of no kind",
    };

    private static bool IsProse(ContentText text) => ProseFields.Contains((text.Owner, text.Property));

    private static void WalkIfAuthored<T>(Result<T> document, string where, List<ContentText> found)
    {
        if (document.IsValid)
        {
            Walk(document.Value, where, string.Empty, found);
        }
    }

    // Every public property, recursively: strings are collected, lists and the dossier types' own
    // objects are walked into, and everything else (numbers, flags, enums) is skipped. A string a
    // document wrote as null is collected as empty, which is what the app shows for it.
    private static void Walk(object value, string where, string floor, List<ContentText> found)
    {
        foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            string path = $"{where}.{property.Name}";
            switch (property.GetValue(value))
            {
                case string text:
                    found.Add(new ContentText(path, property.DeclaringType, property.Name, text, floor));
                    break;

                case null when property.PropertyType == typeof(string):
                    found.Add(new ContentText(path, property.DeclaringType, property.Name, string.Empty, floor));
                    break;

                case IEnumerable items:
                {
                    int index = 0;
                    foreach (object item in items)
                    {
                        if (item is string entry)
                        {
                            found.Add(new ContentText($"{path}[{index}]", property.DeclaringType, property.Name, entry, floor));
                        }
                        else if (IsContent(item))
                        {
                            Walk(item, $"{path}[{index}]", floor, found);
                        }

                        index++;
                    }

                    break;
                }

                case object nested when IsContent(nested):
                    Walk(nested, path, floor, found);
                    break;

                default:
                    break;
            }
        }
    }

    private static bool IsContent(object value) =>
        value is not null && value.GetType().IsClass && value.GetType().Namespace == typeof(AreaDossier).Namespace;
}
