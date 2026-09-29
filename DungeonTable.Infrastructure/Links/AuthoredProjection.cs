using System.Globalization;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Links;

/// <summary>
/// Briefings, reference cards and search from the authored content, with the book index below it:
/// an area's briefing from its own creatures and exits, an entity's
/// card from what its level says about it, a stat block's card from the library, and a book entry's
/// from the book index. Immutable after construction, so it is safe to share as a singleton.
/// </summary>
/// <remarks>
/// An area's lists are the area's word: an empty creature list means nobody is there, and an empty
/// exit list means no way out is worth naming. Nothing is read out of the prose, because a mention is
/// not a relationship.
/// </remarks>
public sealed class AuthoredProjection : IContentProjection
{
    // Search labels for the campaign's own content, beside the book index's "book".
    private const string AreaCategory = SearchMatch.AreaCategory;
    private const string NpcCategory = "npc";
    private const string CharacterCategory = "character";
    private const string MonsterCategory = "monster";
    private const string SpellCategory = "spell";

    // A search hit on one of an area's block headings ranks below every hit on a name or an id.
    private const int HeadingRank = 4;

    private readonly IDossierStore dossiers;
    private readonly IStatLibrary stats;
    private readonly LinkTargets targets;
    private readonly Backlinks backlinks;
    private readonly RelationIndex relations;

    // Every entity a level declares, by id, with the floor its prose belongs to. A later level wins on
    // a duplicate id, as the store does for areas; the link index reports the duplicate as ambiguous.
    private readonly Dictionary<string, (Entity Entity, string Floor)> entities = new(StringComparer.Ordinal);

    // The campaign's own people, by id: an NPC listed in a room fights with the block their entry
    // names, as a level's individuals do.
    private readonly Dictionary<string, NpcDossier> npcs = new(StringComparer.Ordinal);

    // Every book index entry, by id, with its book. The first book to have an id keeps it (the
    // adventure loads first); the link index reports the duplicate as ambiguous.
    private readonly Dictionary<string, (BookEntry Entry, BookIndex Book)> bookEntries = new(StringComparer.Ordinal);

    // Each book's title, by the source file a stat block or spell cites ("SRD_CC_v5.1.pdf").
    private readonly Dictionary<string, string> bookTitles = new(StringComparer.OrdinalIgnoreCase);

    // What search finds by name among the campaign's own content, below the entities: its areas, its
    // people, and the stat blocks and spells.
    private readonly List<SearchMatch> named = new();

    /// <summary>Creates the projection, without a book index.</summary>
    /// <param name="dossiers">The authored content.</param>
    /// <param name="stats">The stat blocks, for the names creatures show and the blocks individuals use.</param>
    /// <param name="targets">Resolves what a creature, exit or stat-block reference names.</param>
    public AuthoredProjection(IDossierStore dossiers, IStatLibrary stats, LinkTargets targets)
        : this(dossiers, stats, targets, Array.Empty<BookIndex>())
    {
    }

    /// <summary>Creates the projection.</summary>
    /// <param name="dossiers">The authored content.</param>
    /// <param name="stats">The stat blocks, for the names creatures show and the blocks individuals use.</param>
    /// <param name="targets">Resolves what a creature, exit or stat-block reference names.</param>
    /// <param name="books">The book index: its entries' cards and search hits, its links as relations, and its titles for citations.</param>
    public AuthoredProjection(IDossierStore dossiers, IStatLibrary stats, LinkTargets targets, IEnumerable<BookIndex> books)
    {
        ArgumentNullException.ThrowIfNull(dossiers);
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(books);
        this.dossiers = dossiers;
        this.stats = stats;
        this.targets = targets;
        IReadOnlyList<BookIndex> loaded = books.Where(book => book is not null).ToArray();
        backlinks = Backlinks.Build(dossiers, targets);
        relations = RelationIndex.Build(dossiers, targets, backlinks, loaded);

        foreach (LevelDossier level in dossiers.AllLevels())
        {
            foreach (Entity entity in level.AllEntities().Where(entity => !string.IsNullOrWhiteSpace(entity.Id)))
            {
                entities[entity.Id] = (entity, level.LevelNodeId);
            }
        }

        Result<NpcRoster> roster = dossiers.GetNpcs();
        if (roster.IsValid)
        {
            foreach (NpcDossier npc in roster.Value.Npcs.Where(npc => npc is not null && !string.IsNullOrWhiteSpace(npc.Id)))
            {
                npcs[npc.Id] = npc;
            }
        }

        foreach (BookIndex book in loaded)
        {
            foreach (BookEntry entry in book.Entries.Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Id)))
            {
                bookEntries.TryAdd(entry.Id, (entry, book));
            }

            if (!string.IsNullOrWhiteSpace(book.Source) && !string.IsNullOrWhiteSpace(book.Title))
            {
                bookTitles.TryAdd(book.Source.Trim(), book.Title);
            }
        }

        IndexNames();
    }

    /// <inheritdoc />
    public Result<RoomBriefing> GetBriefing(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Result.WithMessages<RoomBriefing>(ValidationMessage.Error("A node id is required.", nameof(nodeId)));
        }

        Result<AreaDossier> found = dossiers.GetArea(nodeId);
        if (!found.IsValid)
        {
            return Result.WithMessages<RoomBriefing>(ValidationMessage.Error($"No area '{nodeId}' is authored.", nameof(nodeId)));
        }

        AreaDossier area = found.Value;
        Result<LevelDossier> level = dossiers.GetLevelForArea(nodeId);
        string floor = level.IsValid ? level.Value.LevelNodeId : string.Empty;
        List<OccupantEntry> occupants = Occupants(area, floor);

        return Result.For(new RoomBriefing
        {
            RoomId = area.AreaNodeId,
            Label = area.Title,
            Occupants = occupants,
            Connections = Connections(area, floor),
            Involvements = Involvements(occupants),
        });
    }

    /// <inheritdoc />
    public IReadOnlyList<RelationLine> RelationsOf(string id) => relations.For(id);

    /// <inheritdoc />
    public string StatBlockOf(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        if (entities.TryGetValue(id, out (Entity Entity, string Floor) found))
        {
            return ResolveBlock(found.Entity.StatBlock, found.Floor);
        }

        // An NPC belongs to no floor, and a stat block's name means the same on every one.
        return npcs.TryGetValue(id, out NpcDossier npc) ? ResolveBlock(npc.StatBlock, string.Empty) : string.Empty;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Three tiers, capped together and each ranked by how well the name matches: the entities a
    /// level declares; the campaign's own areas, people, stat blocks and spells, with an area whose
    /// block heading matches ("The Furnace") below every name; and the book index, which is
    /// machine-made and lower-trust. A book entry the campaign has its own entry for is found as that
    /// entry (Wenna Brask opens the NPC card), so it carries the campaign's kind rather than the book
    /// badge.
    /// </remarks>
    public IReadOnlyList<SearchMatch> SearchNodes(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return Array.Empty<SearchMatch>();
        }

        string needle = query.Trim().ToLowerInvariant();
        IEnumerable<SearchMatch> authored = entities.Values
            .Select(found => (Match: EntityMatch(found.Entity), Rank: Rank(found.Entity.Name, found.Entity.Id, needle)))
            .Where(hit => hit.Rank >= 0)
            .OrderBy(hit => hit.Rank)
            .ThenBy(hit => hit.Match.Label.Length)
            .ThenBy(hit => hit.Match.Label, StringComparer.Ordinal)
            .Select(hit => hit.Match);

        IEnumerable<SearchMatch> campaign = named
            .Select(match => (Match: match, Rank: Rank(match.Label, match.Id, needle)))
            .Where(hit => hit.Rank >= 0)
            .Concat(HeadingHits(needle))
            .OrderBy(hit => hit.Rank)
            .ThenBy(hit => hit.Match.Label.Length)
            .ThenBy(hit => hit.Match.Label, StringComparer.Ordinal)
            .Select(hit => hit.Match);

        IEnumerable<SearchMatch> book = bookEntries.Values
            .Select(found => (found.Entry, found.Book, Rank: BookRank(found.Entry, needle)))
            .Where(hit => hit.Rank >= 0)
            .OrderBy(hit => hit.Rank)
            .ThenBy(hit => hit.Entry.Name.Length)
            .ThenBy(hit => hit.Entry.Name, StringComparer.Ordinal)
            .Select(hit => BookMatch(hit.Entry, hit.Book));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return authored
            .Concat(campaign)
            .Concat(book)
            .Where(match => seen.Add(match.Id))
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// An entity a level declares gets its card from the level; a stat block or a spell from the
    /// library; a book index entry nothing authored answers to, from the book index; and an area, a
    /// card of kind <see cref="CrossRefKind.Area"/> saying so, since an area opens in the panel
    /// rather than in a card.
    /// </remarks>
    public Result<ReferenceCard> GetReferenceCard(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Result.WithMessages<ReferenceCard>(ValidationMessage.Error("A node id is required.", nameof(nodeId)));
        }

        if (entities.TryGetValue(nodeId, out (Entity Entity, string Floor) found))
        {
            return Result.For(new ReferenceCard
            {
                NodeId = found.Entity.Id,
                Label = found.Entity.Name.Length > 0 ? found.Entity.Name : found.Entity.Id,
                Kind = LinkTargets.KindOf(found.Entity.Kind),
                Summary = found.Entity.Description ?? string.Empty,
                Areas = backlinks.For(found.Entity.Id),
                StatBlockId = ResolveBlock(found.Entity.StatBlock, found.Floor),
                LevelNodeId = found.Floor,
                Relations = relations.For(found.Entity.Id),
            });
        }

        if (stats.GetMonster(nodeId) is { IsValid: true } monster)
        {
            return Result.For(RulebookCard(nodeId, monster.Value.Name, CrossRefKind.Monster, monster.Value.Source, monster.Value.SourceLocation));
        }

        if (stats.GetSpell(nodeId) is { IsValid: true } spell)
        {
            return Result.For(RulebookCard(nodeId, spell.Value.Name, CrossRefKind.Spell, spell.Value.Source, spell.Value.SourceLocation));
        }

        if (bookEntries.TryGetValue(nodeId, out (BookEntry Entry, BookIndex Book) entry) && IsOwnTarget(entry.Entry))
        {
            return Result.For(BookCard(entry.Entry, entry.Book));
        }

        if (dossiers.GetArea(nodeId) is { IsValid: true } area)
        {
            return Result.For(new ReferenceCard { NodeId = area.Value.AreaNodeId, Label = area.Value.Title, Kind = CrossRefKind.Area });
        }

        return Result.WithMessages<ReferenceCard>(ValidationMessage.Error($"Nothing has the id '{nodeId}'.", nameof(nodeId)));
    }

    /// <inheritdoc />
    public Result<ReferenceCard> GetBookCard(string id) =>
        !string.IsNullOrEmpty(id) && bookEntries.TryGetValue(id, out (BookEntry Entry, BookIndex Book) found)
            ? Result.For(BookCard(found.Entry, found.Book))
            : Result.WithMessages<ReferenceCard>(ValidationMessage.Error($"The book index has no entry '{id}'.", nameof(id)));

    // Where to look an entry up: the book's title, and the page when the conversion found one.
    private static string Citation(BookEntry entry, BookIndex book, bool shortTitle)
    {
        string title = shortTitle ? book.Book.ToUpperInvariant() : book.Title;
        return entry.Page > 0 ? $"{title}, p. {entry.Page.ToString(CultureInfo.InvariantCulture)}" : title;
    }

    // Lower rank sorts first; -1 means no match: exact name, name prefix, name substring, then id.
    private static int Rank(string name, string id, string needle)
    {
        string lower = (name ?? string.Empty).ToLowerInvariant();
        if (lower.Length > 0)
        {
            if (string.Equals(lower, needle, StringComparison.Ordinal))
            {
                return 0;
            }

            if (lower.StartsWith(needle, StringComparison.Ordinal))
            {
                return 1;
            }

            if (lower.Contains(needle, StringComparison.Ordinal))
            {
                return 2;
            }
        }

        return (id ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase) ? 3 : -1;
    }

    // As Rank, and last what the book says about the entry.
    private static int BookRank(BookEntry entry, string needle)
    {
        int rank = Rank(entry.Name, entry.Id, needle);
        if (rank >= 0)
        {
            return rank;
        }

        return (entry.Description ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase) ? 4 : -1;
    }

    private static SearchMatch EntityMatch(Entity entity) => new SearchMatch
    {
        Id = entity.Id,
        Label = entity.Name.Length > 0 ? entity.Name : entity.Id,
        Category = entity.Kind == EntityKind.None ? "entity" : entity.Kind.ToString().ToLowerInvariant(),
    };

    // The campaign's own named content, in load order: areas, then people, then stat blocks and spells.
    private void IndexNames()
    {
        foreach (AreaDossier area in dossiers.AllAreas().Where(area => area is not null && area.AreaNodeId.Length > 0))
        {
            named.Add(new SearchMatch { Id = area.AreaNodeId, Label = area.Title, Category = AreaCategory });
        }

        Result<NpcRoster> roster = dossiers.GetNpcs();
        if (roster.IsValid)
        {
            named.AddRange(roster.Value.Npcs
                .Where(npc => npc is not null && npc.Id.Length > 0)
                .Select(npc => new SearchMatch { Id = npc.Id, Label = npc.Name.Length > 0 ? npc.Name : npc.Id, Category = NpcCategory }));
        }

        Result<PartyDossier> party = dossiers.GetParty();
        if (party.IsValid)
        {
            named.AddRange(party.Value.Characters
                .Where(character => character is not null && character.Id.Length > 0)
                .Select(character => new SearchMatch { Id = character.Id, Label = character.Name.Length > 0 ? character.Name : character.Id, Category = CharacterCategory }));
        }

        named.AddRange(stats.AllMonsters()
            .Where(block => block is not null && block.NodeId.Length > 0)
            .Select(block => new SearchMatch { Id = block.NodeId, Label = block.Name, Category = MonsterCategory }));
        named.AddRange(stats.AllSpells()
            .Where(spell => spell is not null && spell.NodeId.Length > 0)
            .Select(spell => new SearchMatch { Id = spell.NodeId, Label = spell.Name, Category = SpellCategory }));
    }

    // Each area with a block heading that matches, once, labelled with the area's title (what the Room
    // Editor names a region after) and citing the heading that matched, so a room is found by what is
    // in it: "furnace" finds the Bell-Founder's Workshop.
    private IEnumerable<(SearchMatch Match, int Rank)> HeadingHits(string needle)
    {
        foreach (AreaDossier area in dossiers.AllAreas().Where(area => area is not null && area.AreaNodeId.Length > 0))
        {
            DossierBlock block = area.Glance.Concat(area.Detail)
                .FirstOrDefault(block => block is not null && (block.Heading ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase));
            if (block is not null)
            {
                yield return (new SearchMatch { Id = area.AreaNodeId, Label = area.Title, Category = AreaCategory, Citation = block.Heading }, HeadingRank);
            }
        }
    }

    // True when the entry answers to its own id: nothing authored has claimed it.
    private bool IsOwnTarget(BookEntry entry)
    {
        Result<LinkTarget> target = targets.Resolve(entry.Id, string.Empty);
        return target.IsValid && target.Value.Kind == CrossRefKind.Book;
    }

    private SearchMatch BookMatch(BookEntry entry, BookIndex book)
    {
        if (IsOwnTarget(entry))
        {
            return new SearchMatch
            {
                Id = entry.Id,
                Label = entry.Name.Length > 0 ? entry.Name : entry.Id,
                Category = "book",
                Citation = Citation(entry, book, shortTitle: true),
            };
        }

        // The campaign's own entry for the id: found under the book's name, opened as itself.
        Result<LinkTarget> authored = targets.Resolve(entry.Id, string.Empty);
        return new SearchMatch
        {
            Id = authored.IsValid && authored.Value.TargetId.Length > 0 ? authored.Value.TargetId : entry.Id,
            Label = authored.IsValid && authored.Value.DisplayName.Length > 0 ? authored.Value.DisplayName : entry.Name,
            Category = authored.IsValid ? authored.Value.Kind.ToString().ToLowerInvariant() : string.Empty,
        };
    }

    private ReferenceCard BookCard(BookEntry entry, BookIndex book) => new ReferenceCard
    {
        NodeId = entry.Id,
        Label = entry.Name.Length > 0 ? entry.Name : entry.Id,
        Kind = CrossRefKind.Book,
        Summary = entry.Description ?? string.Empty,
        Citation = Citation(entry, book, shortTitle: false),
        Areas = backlinks.For(entry.Id),
        Relations = relations.For(entry.Id),
    };

    // A stat block's or a spell's card: its name, where the book prints it, and the rooms that list or
    // mention it. The drawer shows the block itself beneath.
    private ReferenceCard RulebookCard(string nodeId, string name, CrossRefKind kind, string source, string location) => new ReferenceCard
    {
        NodeId = nodeId,
        Label = name.Length > 0 ? name : nodeId,
        Kind = kind,
        Citation = RulebookCitation(source, location),
        Areas = backlinks.For(nodeId),
    };

    // "SRD 5.1 p.266": the book's title where the book index knows the source file, else the
    // source as written, without its ".pdf".
    private string RulebookCitation(string source, string location)
    {
        string file = (source ?? string.Empty).Trim();
        if (!bookTitles.TryGetValue(file, out string title))
        {
            title = file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
        }

        return $"{title} {(location ?? string.Empty).Trim()}".Trim();
    }

    // Who each of a room's occupants is involved with, once per occupant, in the order they are
    // listed. An occupant that is a kind of creature (four bandits) is never an end: relations are
    // between particular ones.
    private List<Involvement> Involvements(IReadOnlyList<OccupantEntry> occupants) =>
        occupants
            .Where(occupant => occupant.NodeId.Length > 0)
            .GroupBy(occupant => occupant.NodeId, StringComparer.Ordinal)
            .Select(listed => new Involvement
            {
                SubjectId = listed.Key,
                SubjectLabel = listed.First().Label,
                Lines = relations.For(listed.Key),
            })
            .Where(involvement => involvement.Lines.Count > 0)
            .ToList();

    // The node id of the stat block an individual uses, as written in its entity or NPC entry: empty
    // when it names none, or names something that is not a stat block (another individual, a spell).
    // Individuals use blocks; they never are one.
    private string ResolveBlock(string written, string floor)
    {
        if (string.IsNullOrWhiteSpace(written))
        {
            return string.Empty;
        }

        Result<LinkTarget> block = targets.Resolve(written, floor);
        return block.IsValid && stats.HasMonster(block.Value.TargetId) ? block.Value.TargetId : string.Empty;
    }

    private List<OccupantEntry> Occupants(AreaDossier area, string floor) =>
        area.Creatures
            .Where(creature => creature is not null)
            .Select(creature => Occupant(creature, floor))
            .ToList();

    private OccupantEntry Occupant(AreaCreature creature, string floor)
    {
        string written = (creature.Ref ?? string.Empty).Trim();
        Result<LinkTarget> resolved = targets.Resolve(written, floor);
        if (!resolved.IsValid || !resolved.Value.CanBeInARoom)
        {
            // Shown as written, and marked broken by its empty id: fail soft at the table, loud in
            // the content checks. A faction, an item, a spell or an area is not a creature either.
            return new OccupantEntry { Label = written, Count = Count(creature), Note = creature.Note ?? string.Empty, Secret = creature.Secret };
        }

        LinkTarget target = resolved.Value;
        string label = target.DisplayName;
        string block = string.Empty;
        if (entities.TryGetValue(target.TargetId, out (Entity Entity, string Floor) entity))
        {
            label = entity.Entity.Name.Length > 0 ? entity.Entity.Name : entity.Entity.Id;
            block = ResolveBlock(entity.Entity.StatBlock, entity.Floor);
        }
        else if (npcs.TryGetValue(target.TargetId, out NpcDossier npc))
        {
            // One of the campaign's own people, fighting with the block their entry names, if any.
            block = ResolveBlock(npc.StatBlock, string.Empty);
        }
        else if (stats.GetMonster(target.TargetId) is { IsValid: true } monster)
        {
            // A stat block listed as itself: its printed name, not the lower case a link would show.
            label = monster.Value.Name.Length > 0 ? monster.Value.Name : label;
            block = target.TargetId;
        }

        return new OccupantEntry
        {
            NodeId = target.TargetId,
            Label = label,
            MonsterRef = block,
            Count = Count(creature),
            Note = creature.Note ?? string.Empty,
            Secret = creature.Secret,
        };
    }

    private List<ConnectionEntry> Connections(AreaDossier area, string floor) =>
        area.Exits
            .Where(exit => exit is not null)
            .Select(exit => Connection(exit, floor))
            .ToList();

    private ConnectionEntry Connection(AreaExit exit, string floor)
    {
        string written = (exit.To ?? string.Empty).Trim();
        Result<LinkTarget> resolved = targets.Resolve(written, floor);
        string note = exit.Note ?? string.Empty;

        if (resolved.IsValid && resolved.Value.Kind == CrossRefKind.Area)
        {
            Result<AreaDossier> destination = dossiers.GetArea(resolved.Value.TargetId);
            return new ConnectionEntry
            {
                TargetRoomId = resolved.Value.TargetId,
                Label = destination.IsValid && destination.Value.Title.Length > 0 ? destination.Value.Title : written,
                Note = note,
                Secret = exit.Secret,
            };
        }

        // Anything else leads nowhere yet: a floor that is not authored (pending, not an error), a
        // typo, or a target that is not an area at all.
        return new ConnectionEntry
        {
            Label = written,
            Note = note,
            Secret = exit.Secret,
            Pending = resolved.IsValid && resolved.Value.Kind == CrossRefKind.Pending,
        };
    }

    private static string Count(AreaCreature creature) => (creature.Count ?? string.Empty).Trim();
}
