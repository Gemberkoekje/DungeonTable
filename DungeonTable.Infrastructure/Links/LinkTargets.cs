using System.Globalization;
using System.Text.RegularExpressions;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Links;

/// <summary>
/// What the target of an authored <c>[[…]]</c> link names:
/// <list type="bullet">
///   <item>an area, by the key printed on the map: <c>area 6d</c> on the floor the prose belongs to,
///     or <c>L2 area 14</c> on a floor named by the <c>n</c> of its <c>level-{n}.json</c>;</item>
///   <item>a person, by the id the NPC roster or the party gives them (<c>wenna-brask</c>);</item>
///   <item>an entity a level declares, by its id (<c>bell-warden</c>, <c>wickfoot-goblins</c>);</item>
///   <item>a stat block or a spell, by the slug of its name (<c>bandit</c>, <c>flesh-golem</c>);</item>
///   <item>an entry in the book index, by its id (<c>the-old-mill</c>), where nothing authored has
///     that name: the campaign's own content always wins over the machine-made index.</item>
/// </list>
/// Built once from the loaded content and immutable afterwards, so it is safe to share as a
/// singleton. An area's creature and exit lists name their targets the same way.
/// </summary>
/// <remarks>
/// <para>
/// Names are matched as slugs, so <c>[[flesh-golem]]</c>, <c>[[Flesh Golem]]</c> and
/// <c>[[flesh golem]]</c> all name the same stat block, and an apostrophe is dropped rather than
/// split on (<c>grasks-crew</c> for "Grask's crew").
/// </para>
/// <para>
/// A target that could mean two things resolves to neither: a guessed link is worse than a broken
/// one, because nothing flags it. <see cref="Collisions"/> lists every such name, which is what the
/// content checks assert is empty.
/// </para>
/// <para>
/// An area on a floor that has no level file yet (<c>L2 area 14</c> while only Level 1 is authored)
/// is <see cref="CrossRefKind.Pending"/>, not broken: it cannot be checked, and it is meant to become
/// a jump by itself the day that floor is loaded, like a quest beat's destination.
/// </para>
/// </remarks>
public sealed partial class LinkTargets
{
    private readonly Dictionary<string, List<Entry>> byName;
    private readonly Dictionary<string, List<Entry>> bookEntries;
    private readonly Dictionary<string, Dictionary<string, List<Entry>>> areasByFloor;
    private readonly Dictionary<int, List<string>> floorsByNumber;

    private LinkTargets(
        Dictionary<string, List<Entry>> byName,
        Dictionary<string, List<Entry>> bookEntries,
        Dictionary<string, Dictionary<string, List<Entry>>> areasByFloor,
        Dictionary<int, List<string>> floorsByNumber)
    {
        this.byName = byName;
        this.bookEntries = bookEntries;
        this.areasByFloor = areasByFloor;
        this.floorsByNumber = floorsByNumber;
    }

    /// <summary>Indexes everything a link can name in the loaded content, without a book index.</summary>
    /// <param name="dossiers">Supplies the areas with their floors, and the NPC roster and the party.</param>
    /// <param name="stats">Supplies the stat blocks and the spells.</param>
    /// <returns>The index.</returns>
    public static LinkTargets Build(IDossierStore dossiers, IStatLibrary stats) =>
        Build(dossiers, stats, Array.Empty<BookIndex>());

    /// <summary>Indexes everything a link can name in the loaded content.</summary>
    /// <param name="dossiers">Supplies the areas with their floors, and the NPC roster and the party.</param>
    /// <param name="stats">Supplies the stat blocks and the spells.</param>
    /// <param name="books">
    /// The book index. An entry whose id something authored already answers to only extends that
    /// thing, so it is not a target of its own.
    /// </param>
    /// <returns>The index.</returns>
    public static LinkTargets Build(IDossierStore dossiers, IStatLibrary stats, IEnumerable<BookIndex> books)
    {
        ArgumentNullException.ThrowIfNull(dossiers);
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(books);

        var byName = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        foreach (StatBlock monster in stats.AllMonsters())
        {
            Add(byName, Slug(monster.Name), new Entry(monster.NodeId, CrossRefKind.Monster, monster.Name, $"the {monster.Name} stat block", CommonNoun: true));
        }

        foreach (SpellEntry spell in stats.AllSpells())
        {
            Add(byName, Slug(spell.Name), new Entry(spell.NodeId, CrossRefKind.Spell, spell.Name, $"the {spell.Name} spell", CommonNoun: true));
        }

        Result<NpcRoster> npcs = dossiers.GetNpcs();
        if (npcs.IsValid)
        {
            foreach (NpcDossier npc in npcs.Value.Npcs)
            {
                Add(byName, Slug(npc.Id), new Entry(npc.Id, CrossRefKind.Npc, npc.Name, $"the NPC '{npc.Id}'", CommonNoun: false));
            }
        }

        Result<PartyDossier> party = dossiers.GetParty();
        if (party.IsValid)
        {
            foreach (CharacterDossier character in party.Value.Characters)
            {
                Add(byName, Slug(character.Id), new Entry(character.Id, CrossRefKind.Character, character.Name, $"the player character '{character.Id}'", CommonNoun: false));
            }
        }

        // An entity is named, like a person: "the Bell-Warden" is one gargoyle, not a kind of thing.
        foreach (LevelDossier level in dossiers.AllLevels())
        {
            foreach (Entity entity in level.AllEntities())
            {
                Add(byName, Slug(entity.Id), new Entry(entity.Id, KindOf(entity.Kind), entity.Name, $"the entity '{entity.Id}' on {level.LevelNodeId}", CommonNoun: false));
            }
        }

        var areasByFloor = new Dictionary<string, Dictionary<string, List<Entry>>>(StringComparer.Ordinal);
        foreach (AreaDossier area in dossiers.AllAreas())
        {
            // An area belongs to the floor whose file it was read from. One on no floor at all has
            // nothing to be named by: "area 6d" only means something relative to a floor.
            Result<LevelDossier> level = dossiers.GetLevelForArea(area.AreaNodeId);
            string key = PrintedKey(area);
            if (!level.IsValid || string.IsNullOrEmpty(level.Value.LevelNodeId) || key.Length == 0)
            {
                continue;
            }

            if (!areasByFloor.TryGetValue(level.Value.LevelNodeId, out Dictionary<string, List<Entry>> keys))
            {
                keys = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
                areasByFloor[level.Value.LevelNodeId] = keys;
            }

            Add(keys, key, new Entry(area.AreaNodeId, CrossRefKind.Area, area.Title, area.Title, CommonNoun: false));
        }

        var floorsByNumber = new Dictionary<int, List<string>>();
        foreach (string levelNodeId in dossiers.AllLevels().Select(level => level.LevelNodeId))
        {
            Result<int> number = dossiers.GetLevelNumber(levelNodeId);
            if (!number.IsValid)
            {
                continue;
            }

            if (!floorsByNumber.TryGetValue(number.Value, out List<string> floors))
            {
                floors = new List<string>();
                floorsByNumber[number.Value] = floors;
            }

            if (!floors.Contains(levelNodeId, StringComparer.Ordinal))
            {
                floors.Add(levelNodeId);
            }
        }

        // Below everything authored: the campaign's own entry for a name always wins.
        var bookEntries = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        foreach (BookIndex book in books.Where(book => book is not null))
        {
            foreach (BookEntry entry in book.Entries.Where(entry => entry is not null))
            {
                string key = Slug(entry.Id);
                if (!byName.ContainsKey(key))
                {
                    Add(bookEntries, key, new Entry(entry.Id, CrossRefKind.Book, entry.Name, $"the book entry '{entry.Id}' in {book.Book}", CommonNoun: false));
                }
            }
        }

        return new LinkTargets(byName, bookEntries, areasByFloor, floorsByNumber);
    }

    /// <summary>Resolves one link target.</summary>
    /// <param name="target">The target as written between the brackets ("bandit", "area 6d").</param>
    /// <param name="levelNodeId">
    /// The floor the prose belongs to, which a bare <c>area 6d</c> is read on; an empty id for prose
    /// that belongs to no floor, where only <c>L1 area 6d</c> can name an area.
    /// </param>
    /// <returns>What the target names, or an invalid result saying why it names nothing.</returns>
    public Result<LinkTarget> Resolve(string target, string levelNodeId)
    {
        string written = (target ?? string.Empty).Trim();
        if (written.Length == 0)
        {
            return Failure("The link has no target: write [[target]] or [[target|shown text]].");
        }

        Match area = AreaTarget().Match(Normalize(written));
        if (area.Success)
        {
            return ResolveArea(written, area, levelNodeId ?? string.Empty);
        }

        if (!byName.TryGetValue(Slug(written), out List<Entry> found)
            && !bookEntries.TryGetValue(Slug(written), out found))
        {
            return Failure($"[[{written}]] names nothing that is loaded: no stat block, spell, entity, NPC, player character or book entry has that name or id.");
        }

        if (found.Count > 1)
        {
            return Failure($"[[{written}]] is ambiguous: it could mean {Describe(found)}.");
        }

        Entry entry = found[0];
        return Result.For(new LinkTarget
        {
            TargetId = entry.TargetId,
            Kind = entry.Kind,
            DisplayName = DisplayName(entry, written),
            Rulebook = entry.CommonNoun,
        });
    }

    /// <summary>
    /// The key an area is printed under on the map, read from its title's "(Level 1, Area 8b)" tag:
    /// "8b". For a line that says where someone is without spelling out the room's whole name.
    /// </summary>
    /// <param name="title">An area's title.</param>
    /// <returns>The printed key, or an empty string when the title carries none.</returns>
    public static string PrintedKeyOf(string title)
    {
        Match printed = PrintedAreaKey().Match(title ?? string.Empty);
        return printed.Success ? printed.Groups[1].Value.ToLowerInvariant() : string.Empty;
    }

    /// <summary>
    /// Every name that means more than one thing: an id or slug two entries share, a printed area key
    /// that appears twice on one floor, or a floor number two level files claim. A link to any of
    /// them cannot resolve, so the content checks require this to be empty.
    /// </summary>
    /// <returns>One sentence per collision (possibly empty).</returns>
    public IReadOnlyList<string> Collisions()
    {
        var collisions = new List<string>();
        foreach (KeyValuePair<string, List<Entry>> name in byName.Concat(bookEntries).Where(pair => pair.Value.Count > 1))
        {
            collisions.Add($"[[{name.Key}]] could mean {Describe(name.Value)}.");
        }

        foreach (KeyValuePair<string, Dictionary<string, List<Entry>>> floor in areasByFloor)
        {
            foreach (KeyValuePair<string, List<Entry>> key in floor.Value.Where(pair => pair.Value.Count > 1))
            {
                collisions.Add($"'area {key.Key}' on {floor.Key} could mean {Describe(key.Value)}.");
            }
        }

        foreach (KeyValuePair<int, List<string>> number in floorsByNumber.Where(pair => pair.Value.Count > 1))
        {
            collisions.Add($"'L{number.Key}' could mean any of {string.Join(", ", number.Value)}: two level files carry the number {number.Key}.");
        }

        return collisions;
    }

    /// <summary>
    /// The form link targets are compared in: lower case, apostrophes dropped, and every other run of
    /// characters that are not letters or digits turned into one hyphen ("Grask's Crew" becomes
    /// <c>grasks-crew</c>).
    /// </summary>
    /// <param name="name">A name or id.</param>
    /// <returns>The slug; empty when nothing usable is left.</returns>
    public static string Slug(string name)
    {
        string lower = (name ?? string.Empty).ToLowerInvariant().Replace("'", string.Empty, StringComparison.Ordinal).Replace("’", string.Empty, StringComparison.Ordinal);
        return NotSlug().Replace(lower, "-").Trim('-');
    }

    private Result<LinkTarget> ResolveArea(string written, Match area, string levelNodeId)
    {
        string floor = levelNodeId;
        if (area.Groups["level"].Success)
        {
            int number = int.Parse(area.Groups["level"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            if (!floorsByNumber.TryGetValue(number, out List<string> floors))
            {
                // Nothing to check it against yet, and nothing wrong with writing it early.
                return number > 0
                    ? Result.For(new LinkTarget { TargetId = string.Empty, Kind = CrossRefKind.Pending, DisplayName = written })
                    : Failure($"[[{written}]] names level {number}, and floors are numbered from 1.");
            }

            if (floors.Count > 1)
            {
                return Failure($"[[{written}]] is ambiguous: level {number} is more than one file ({string.Join(", ", floors)}).");
            }

            floor = floors[0];
        }
        else if (floor.Length == 0)
        {
            return Failure($"[[{written}]] needs a floor: this prose belongs to no level, so write it as [[L1 {written}]] with the level number.");
        }

        string key = area.Groups["key"].Value;
        if (!areasByFloor.TryGetValue(floor, out Dictionary<string, List<Entry>> keys)
            || !keys.TryGetValue(key, out List<Entry> found))
        {
            return Failure($"[[{written}]] names no authored area: {floor} has no area {key}.");
        }

        if (found.Count > 1)
        {
            return Failure($"[[{written}]] is ambiguous: it could mean {Describe(found)}.");
        }

        return Result.For(new LinkTarget
        {
            TargetId = found[0].TargetId,
            Kind = CrossRefKind.Area,
            DisplayName = written,
        });
    }

    /// <summary>
    /// The link kind an entity reads as: an individual creature is coloured and opened like a
    /// monster, and the rest like the thing they are.
    /// </summary>
    /// <param name="kind">The entity's kind.</param>
    /// <returns>The matching link kind; <see cref="CrossRefKind.None"/> for an entity with no kind.</returns>
    public static CrossRefKind KindOf(EntityKind kind) => kind switch
    {
        EntityKind.Creature => CrossRefKind.Monster,
        EntityKind.Npc => CrossRefKind.Npc,
        EntityKind.Faction => CrossRefKind.Faction,
        EntityKind.Item => CrossRefKind.Item,
        _ => CrossRefKind.None,
    };

    // The words a [[target]] without shown text displays. People and entities are proper names, so
    // they show as named. A stat block is a common noun in running prose ("a flesh golem"), so it
    // shows in lower case, unless the author capitalised the target: [[Gargoyle]] asks for the name
    // as printed.
    private static string DisplayName(Entry entry, string written)
    {
        if (string.IsNullOrEmpty(entry.Name))
        {
            return written;
        }

        if (entry.CommonNoun && !char.IsUpper(written[0]))
        {
            return entry.Name.ToLowerInvariant();
        }

        return entry.Name;
    }

    // The key an area is printed under on the map: the "Area 2a" in its title's "(Level 1, Area 2a)"
    // tag, or its area number for a title written without one.
    private static string PrintedKey(AreaDossier area)
    {
        Match printed = PrintedAreaKey().Match(area.Title ?? string.Empty);
        if (printed.Success)
        {
            return printed.Groups[1].Value.ToLowerInvariant();
        }

        return area.AreaNumber > 0 ? area.AreaNumber.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    // Lower case, with runs of spaces, hyphens and underscores folded to one space, so "L2-area-14"
    // and "l2  area 14" read the same as "L2 area 14".
    private static string Normalize(string written) =>
        Separators().Replace(written.ToLowerInvariant(), " ").Trim();

    private static void Add(Dictionary<string, List<Entry>> index, string key, Entry entry)
    {
        // Hand-edited JSON can carry an explicit null where the Core type defaults to "".
        if (string.IsNullOrEmpty(entry.TargetId) || string.IsNullOrEmpty(key))
        {
            return;
        }

        if (!index.TryGetValue(key, out List<Entry> entries))
        {
            entries = new List<Entry>();
            index[key] = entries;
        }

        entries.Add(entry);
    }

    private static string Describe(List<Entry> entries) =>
        string.Join(" or ", entries.Select(entry => entry.Source));

    private static Result<LinkTarget> Failure(string message) =>
        Result.WithMessages<LinkTarget>(ValidationMessage.Error(message, "target"));

    [GeneratedRegex(@"^(?:l(?<level>[0-9]{1,4}) )?area (?<key>[0-9]{1,4}[a-z]?)$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex AreaTarget();

    [GeneratedRegex(@"\bArea\s+([0-9]+[A-Za-z]?)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex PrintedAreaKey();

    [GeneratedRegex(@"[\s\-_]+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Separators();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NotSlug();

    // One thing a name can mean. Source is how a collision message describes it; CommonNoun is set for
    // the rulebook entries whose names read in lower case in running prose.
    private sealed record Entry(string TargetId, CrossRefKind Kind, string Name, string Source, bool CommonNoun);
}
