using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Content;
using DungeonTable.Infrastructure.Dossiers;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Stats;

/// <summary>
/// File-system adapter for <see cref="IStatLibrary"/>. Reads every <c>monsters*.json</c> (a
/// serialized <see cref="StatBlock"/> array) and every <c>spells*.json</c> (a serialized
/// <see cref="SpellEntry"/> array) from the stat-block root once at construction, indexing both by
/// node id. Immutable after construction, so this is safe to use as a singleton. A document that
/// fails to parse is skipped rather than throwing, so a corrupt file cannot take down the whole store,
/// and a <c>null</c> entry or list in one that does parse is read as nothing, as in the dossiers.
/// </summary>
/// <remarks>
/// Several documents of each kind merge, so a published library such as <c>monsters-srd.json</c>
/// sits beside a campaign's own <c>monsters.json</c> and neither has to be edited to add to the
/// other. They load in ordinal file-name order and a later document wins on a duplicate node id, the
/// rule the quest documents follow. '-' (0x2D) sorts before '.' (0x2E), so
/// <c>monsters-srd.json</c> loads before <c>monsters.json</c>: a campaign's own block replaces the
/// library's under the same node id.
/// </remarks>
public sealed class FileSystemStatLibrary : IStatLibrary
{
    // The patterns and the reading options below are internal so the content tests can read every
    // document the way this library does and report what it would skip.
    internal const string MonstersFilePattern = "monsters*.json";
    internal const string SpellsFilePattern = "spells*.json";

    // Both patterns, for the report of a .json the library would not read.
    private static readonly string[] AllNames = { MonstersFilePattern, SpellsFilePattern };

    /// <summary>Category chip shown for a stat-library search match.</summary>
    private const string MonsterCategory = "monster";

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,

        // Hand-edited like the dossiers, and a homebrew block is as likely to be written by an LLM: a
        // null reads as if the field were left out, as in the dossiers.
        TypeInfoResolver = NullMeansLeftOut.Resolver(),
        Converters =
        {
            new NullTolerantValueConverter(),

            // A null list inside a block is an empty one and a null entry is dropped. The document's
            // own list is a List<T>, which the converter does not claim; the constructor skips a null
            // entry in it itself.
            new LenientListConverter(),

            // "challengeRating": 2 reads as "2", as a number in a dossier's text field does.
            new LenientStringConverter(),
        },
    };

    private readonly Dictionary<string, StatBlock> monstersByNode = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpellEntry> spellsByNode = new(StringComparer.Ordinal);

    // Snapshotted once loading is done, so a caller that indexes the whole set neither re-enumerates
    // a dictionary nor sees an entry a later duplicate replaced.
    private readonly IReadOnlyList<StatBlock> allMonsters = Array.Empty<StatBlock>();
    private readonly IReadOnlyList<SpellEntry> allSpells = Array.Empty<SpellEntry>();

    private readonly List<ContentProblem> problems = new List<ContentProblem>();

    /// <summary>Creates a library over a stat-block root directory, loading and indexing its documents.</summary>
    /// <param name="statsRoot">Absolute path to the directory holding the stat-block documents.</param>
    public FileSystemStatLibrary(string statsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statsRoot);
        if (!Directory.Exists(statsRoot))
        {
            return;
        }

        foreach (string monstersPath in Documents(statsRoot, MonstersFilePattern))
        {
            if (TryRead<List<StatBlock>>(monstersPath, out List<StatBlock> monsters))
            {
                Index(monstersPath, monsters, monster => monster.NodeId, monster => monster.Name, monstersByNode);
            }
        }

        foreach (string spellsPath in Documents(statsRoot, SpellsFilePattern))
        {
            if (TryRead<List<SpellEntry>>(spellsPath, out List<SpellEntry> spells))
            {
                Index(spellsPath, spells, spell => spell.NodeId, spell => spell.Name, spellsByNode);
            }
        }

        allMonsters = monstersByNode.Values.ToArray();
        allSpells = spellsByNode.Values.ToArray();
        problems.AddRange(DocumentNames.Unread(statsRoot, AllNames));
    }

    /// <summary>
    /// What the library skipped or dropped while reading its documents, file by file: a document that
    /// does not parse, an entry with no node id, a node id written twice in one document, a <c>null</c>
    /// entry, and a <c>.json</c> under a name the library does not read. A campaign's block replacing
    /// the SRD's under the same node id, from another document, is the point of the merge and is not
    /// one. Empty when everything loaded.
    /// </summary>
    public IReadOnlyList<ContentProblem> Problems => problems;

    /// <inheritdoc />
    public IReadOnlyList<StatBlock> AllMonsters() => allMonsters;

    /// <inheritdoc />
    public IReadOnlyList<SpellEntry> AllSpells() => allSpells;

    /// <inheritdoc />
    public Result<StatBlock> GetMonster(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Result.WithMessages<StatBlock>(
                ValidationMessage.Error("A monster node id is required.", nameof(nodeId)));
        }

        return monstersByNode.TryGetValue(nodeId, out StatBlock monster)
            ? Result.For(monster)
            : Result.WithMessages<StatBlock>(
                ValidationMessage.Error($"No stat block is extracted for '{nodeId}'.", nameof(nodeId)));
    }

    /// <inheritdoc />
    public Result<SpellEntry> GetSpell(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Result.WithMessages<SpellEntry>(
                ValidationMessage.Error("A spell node id is required.", nameof(nodeId)));
        }

        return spellsByNode.TryGetValue(nodeId, out SpellEntry spell)
            ? Result.For(spell)
            : Result.WithMessages<SpellEntry>(
                ValidationMessage.Error($"No spell entry is extracted for '{nodeId}'.", nameof(nodeId)));
    }

    /// <inheritdoc />
    public bool HasMonster(string nodeId) =>
        !string.IsNullOrWhiteSpace(nodeId) && monstersByNode.ContainsKey(nodeId);

    /// <inheritdoc />
    public IReadOnlyList<SearchMatch> SearchMonsters(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return Array.Empty<SearchMatch>();
        }

        string needle = query.Trim().ToLowerInvariant();

        return monstersByNode
            .Select(entry => (entry.Key, entry.Value, rank: Rank(entry.Key, entry.Value, needle)))
            .Where(entry => entry.rank >= 0)
            .OrderBy(entry => entry.rank)
            .ThenBy(entry => entry.Value.Name.Length)
            .ThenBy(entry => entry.Value.Name, StringComparer.Ordinal)
            .Take(limit)
            .Select(entry => new SearchMatch
            {
                Id = entry.Key,
                Label = entry.Value.Name,
                Category = MonsterCategory,
            })
            .ToList();
    }

    // Lower rank sorts first; -1 means no match. Mirrors the dossier search convention.
    private static int Rank(string nodeId, StatBlock monster, string needle)
    {
        string name = monster.Name.ToLowerInvariant();
        if (name.Length > 0)
        {
            if (string.Equals(name, needle, StringComparison.Ordinal))
            {
                return 0;
            }

            if (name.StartsWith(needle, StringComparison.Ordinal))
            {
                return 1;
            }

            if (name.Contains(needle, StringComparison.Ordinal))
            {
                return 2;
            }
        }

        return nodeId.Contains(needle, StringComparison.OrdinalIgnoreCase) ? 3 : -1;
    }

    // Every matching document, in a stable order. Which document is last decides a duplicate node id,
    // and Directory.EnumerateFiles promises no order at all, so without this the winner could differ
    // between two machines reading the same files. Ordinal, as the dossier store orders its documents.
    private static IEnumerable<string> Documents(string statsRoot, string pattern) =>
        Directory
            .EnumerateFiles(statsRoot, pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal);

    // Indexes one document's entries by node id, skipping a null one (the document's own list is a
    // List<T>, which the list converter does not claim) and reporting one with no id or with an id
    // this document already gave.
    private void Index<T>(string path, List<T> entries, Func<T, string> idOf, Func<T, string> nameOf, Dictionary<string, T> into)
        where T : class
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            T entry = entries[i];
            if (entry is null)
            {
                continue;
            }

            string id = idOf(entry);
            if (string.IsNullOrEmpty(id))
            {
                Report(path, $"[{i}] ('{nameOf(entry)}') has no nodeId, so the app dropped it.");
                continue;
            }

            if (!seen.Add(id))
            {
                Report(path, $"'{id}' is written twice in this file; the app keeps the later one.");
            }

            into[id] = entry;
        }
    }

    // Reads one document, reporting it when it cannot be read (it is then skipped) and each null entry
    // the reading drops from it.
    private bool TryRead<T>(string path, out T value)
        where T : class
    {
        value = null;
        string reason;
        try
        {
            string json = File.ReadAllText(path);
            T parsed = JsonSerializer.Deserialize<T>(json, Options);
            if (parsed is not null)
            {
                foreach (string entry in NullEntries.Described(json))
                {
                    Report(path, entry);
                }

                value = parsed;
                return true;
            }

            reason = "it holds nothing but null";
        }
        catch (JsonException error)
        {
            reason = error.Message;
        }
        catch (IOException error)
        {
            reason = error.Message;
        }
        catch (UnauthorizedAccessException error)
        {
            reason = error.Message;
        }

        problems.Add(ContentProblem.Unreadable(path, reason));
        return false;
    }

    private void Report(string file, string problem) => problems.Add(new ContentProblem(file, problem));
}
