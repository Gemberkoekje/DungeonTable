using System.Globalization;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Content;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Dossiers;

/// <summary>
/// File-system adapter for <see cref="IDossierStore"/>. Reads every <c>level-{n}.json</c>
/// (a serialized <see cref="LevelDossierSet"/>), the optional <c>reference.json</c>
/// (a serialized <see cref="ReferenceLibrary"/>), <c>characters.json</c>
/// (a serialized <see cref="PartyDossier"/>), <c>npcs.json</c>
/// (a serialized <see cref="NpcRoster"/>), <c>story.json</c>
/// (a serialized <see cref="StoryLibrary"/>), <c>sessions.json</c>
/// (a serialized <see cref="SessionLog"/>), <c>relations.json</c>
/// (a serialized <see cref="RelationList"/>), every <c>quests*.json</c>
/// (a serialized <see cref="QuestLog"/>) and every <c>deck-*.json</c>
/// (a serialized <see cref="CardDeck"/>) from the dossiers root once at construction,
/// indexing areas and levels by node id. Immutable after construction, so this is safe to
/// use as a singleton. A file that fails to parse is skipped rather than throwing, so one corrupt
/// document cannot take down the whole store. In one that does parse, a <c>null</c> list is read as
/// empty, a <c>null</c> entry in a list is dropped, and a <c>null</c> level is a level with no id.
/// </summary>
public sealed class FileSystemDossierStore : IDossierStore
{
    // The file names and patterns, and the reading options below, are internal so the content tests
    // can read every document the way this store does and report what it would skip.
    internal const string LevelFilePattern = "level-*.json";
    internal const string ReferenceFileName = "reference.json";
    internal const string CampaignFileName = "campaign.json";
    internal const string CharactersFileName = "characters.json";
    internal const string StoryFileName = "story.json";
    internal const string NpcsFileName = "npcs.json";
    internal const string SessionsFileName = "sessions.json";
    internal const string RelationsFileName = "relations.json";

    // Every card deck, one document each. A deck is keyed by the id it declares, not by its file
    // name, and one that declares no id is skipped: the same fail-soft rule the other documents follow.
    internal const string DeckFilePattern = "deck-*.json";

    // Every quest document, not just "quests.json": a second adventure's quest set or a level's own
    // side quests drop in beside it and merge, the way level-*.json already does.
    internal const string QuestFilePattern = "quests*.json";

    // Every name and pattern this store reads, for the report of a .json it would not read.
    internal static readonly string[] AllNames =
    {
        LevelFilePattern, ReferenceFileName, CampaignFileName, CharactersFileName, NpcsFileName,
        StoryFileName, SessionsFileName, RelationsFileName, QuestFilePattern, DeckFilePattern,
    };

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,

        // These documents are written by hand and by LLMs, which say "nothing here" with null: a null
        // title, name or object reads as if it were left out. See NullMeansLeftOut.
        TypeInfoResolver = NullMeansLeftOut.Resolver(),
        Converters =
        {
            // Enums in camelCase, and a null number, flag or enum read as its default.
            new NullTolerantValueConverter(),

            // An explicit "quests": null overwrites the Core type's Array.Empty default, and the
            // deserializer still reports success, so the null surfaces as a crash at the first
            // dereference. See LenientListConverter.
            new LenientListConverter(),

            // A creature's "count": 4 is a number in the JSON and a string in the model, because
            // "1d4+1" is a count too.
            new LenientStringConverter(),
        },
    };

    private readonly Dictionary<string, AreaDossier> areasByNode = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LevelDossier> levelsByNode = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LevelDossier> levelByArea = new(StringComparer.Ordinal);

    // Each level's floor number, from the level-{n}.json it was read from. Kept beside the dossier
    // rather than on it: the number belongs to the file, and LevelDossier mirrors the JSON inside it.
    private readonly Dictionary<string, int> levelNumbers = new(StringComparer.Ordinal);
    private readonly ReferenceLibrary reference = new ReferenceLibrary();
    private readonly bool hasReference;

    // The merged quest log, built once from every quests*.json found. A later document wins on a
    // duplicate id, matching how the level files index areas.
    private readonly QuestLog quests = new QuestLog();
    private readonly bool hasQuests;

    private readonly CampaignDossier campaign = new CampaignDossier();
    private readonly bool hasCampaign;

    private readonly PartyDossier party = new PartyDossier();
    private readonly bool hasParty;

    private readonly StoryLibrary story = new StoryLibrary();
    private readonly bool hasStory;

    private readonly NpcRoster npcs = new NpcRoster();
    private readonly bool hasNpcs;

    private readonly SessionLog sessions = new SessionLog();
    private readonly bool hasSessions;

    private readonly RelationList relations = new RelationList();
    private readonly bool hasRelations;

    // Every deck, in file-name order; a later file wins on a duplicate id, as the quest log does.
    private readonly IReadOnlyList<CardDeck> decks = Array.Empty<CardDeck>();

    // Snapshotted once the indexing loop is done, so callers that index the whole set (the
    // cross-reference engine) neither re-enumerate a dictionary nor see a duplicated area.
    private readonly IReadOnlyList<AreaDossier> allAreas = Array.Empty<AreaDossier>();

    // Snapshotted alongside allAreas, for the callers that index every floor's own blocks rather
    // than look one floor up by id.
    private readonly IReadOnlyList<LevelDossier> allLevels = Array.Empty<LevelDossier>();

    // What was skipped or dropped while reading, and the file each area and level was read from, so a
    // second one under the same id can say where the first is.
    private readonly List<ContentProblem> problems = new List<ContentProblem>();
    private readonly Dictionary<string, string> areaFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> levelFiles = new(StringComparer.Ordinal);

    /// <summary>Creates a store over a dossiers root directory, loading and indexing its documents.</summary>
    /// <param name="dossiersRoot">Absolute path to the directory holding the dossier documents.</param>
    public FileSystemDossierStore(string dossiersRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dossiersRoot);
        if (!Directory.Exists(dossiersRoot))
        {
            return;
        }

        foreach (string path in Documents(dossiersRoot, LevelFilePattern))
        {
            IndexLevelFile(path);
        }

        allAreas = areasByNode.Values.ToArray();
        allLevels = levelsByNode.Values.ToArray();

        string referencePath = Path.Combine(dossiersRoot, ReferenceFileName);
        if (File.Exists(referencePath) && TryRead<ReferenceLibrary>(referencePath, out ReferenceLibrary library))
        {
            reference = library;
            hasReference = true;
        }

        var merged = new List<(Quest Quest, string File)>();
        foreach (string path in Documents(dossiersRoot, QuestFilePattern))
        {
            if (TryRead<QuestLog>(path, out QuestLog log))
            {
                hasQuests = true;
                for (int i = 0; i < log.Quests.Count; i++)
                {
                    Quest quest = log.Quests[i];
                    if (string.IsNullOrEmpty(quest.Id))
                    {
                        Report(path, $"quests[{i}] ('{quest.Title}') has no id, so the app dropped it.");
                        continue;
                    }

                    merged.Add((quest, path));
                }
            }
        }

        quests = new QuestLog { Quests = Dedupe(merged, quest => quest.Id, "quest") };

        string campaignPath = Path.Combine(dossiersRoot, CampaignFileName);
        if (File.Exists(campaignPath) && TryRead<CampaignDossier>(campaignPath, out CampaignDossier background))
        {
            campaign = background;
            hasCampaign = true;
        }

        string charactersPath = Path.Combine(dossiersRoot, CharactersFileName);
        if (File.Exists(charactersPath) && TryRead<PartyDossier>(charactersPath, out PartyDossier roster))
        {
            party = roster;
            hasParty = true;
        }

        string storyPath = Path.Combine(dossiersRoot, StoryFileName);
        if (File.Exists(storyPath) && TryRead<StoryLibrary>(storyPath, out StoryLibrary history))
        {
            story = history;
            hasStory = true;
        }

        string npcsPath = Path.Combine(dossiersRoot, NpcsFileName);
        if (File.Exists(npcsPath) && TryRead<NpcRoster>(npcsPath, out NpcRoster people))
        {
            npcs = people;
            hasNpcs = true;
        }

        string sessionsPath = Path.Combine(dossiersRoot, SessionsFileName);
        if (File.Exists(sessionsPath) && TryRead<SessionLog>(sessionsPath, out SessionLog prepped))
        {
            sessions = prepped;
            hasSessions = true;
        }

        string relationsPath = Path.Combine(dossiersRoot, RelationsFileName);
        if (File.Exists(relationsPath) && TryRead<RelationList>(relationsPath, out RelationList threads))
        {
            relations = threads;
            hasRelations = true;
        }

        var authoredDecks = new List<(CardDeck Deck, string File)>();
        foreach (string path in Documents(dossiersRoot, DeckFilePattern))
        {
            if (!TryRead<CardDeck>(path, out CardDeck deck))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(deck.Id))
            {
                Report(path, $"the deck ('{deck.Title}') has no id, so the app skipped it: drawn cards are saved under its id.");
                continue;
            }

            authoredDecks.Add((deck, path));
        }

        decks = Dedupe(authoredDecks, deck => deck.Id, "deck");
        problems.AddRange(DocumentNames.Unread(dossiersRoot, AllNames));
    }

    /// <summary>
    /// What the store skipped or dropped while reading its documents, file by file: a document that
    /// does not parse, an area, quest or deck with no id, an id written twice, a <c>null</c> in a list,
    /// and a <c>.json</c> under a name the store does not read. Empty when everything loaded.
    /// </summary>
    public IReadOnlyList<ContentProblem> Problems => problems;

    /// <inheritdoc />
    public Result<AreaDossier> GetArea(string areaNodeId)
    {
        if (string.IsNullOrWhiteSpace(areaNodeId))
        {
            return Result.WithMessages<AreaDossier>(
                ValidationMessage.Error("An area node id is required.", nameof(areaNodeId)));
        }

        return areasByNode.TryGetValue(areaNodeId, out AreaDossier area)
            ? Result.For(area)
            : Result.WithMessages<AreaDossier>(
                ValidationMessage.Error($"No dossier is authored for area '{areaNodeId}'.", nameof(areaNodeId)));
    }

    /// <inheritdoc />
    public Result<LevelDossier> GetLevel(string levelNodeId)
    {
        if (string.IsNullOrWhiteSpace(levelNodeId))
        {
            return Result.WithMessages<LevelDossier>(
                ValidationMessage.Error("A level node id is required.", nameof(levelNodeId)));
        }

        return levelsByNode.TryGetValue(levelNodeId, out LevelDossier level)
            ? Result.For(level)
            : Result.WithMessages<LevelDossier>(
                ValidationMessage.Error($"No dossier is authored for level '{levelNodeId}'.", nameof(levelNodeId)));
    }

    /// <inheritdoc />
    public Result<LevelDossier> GetLevelForArea(string areaNodeId)
    {
        if (string.IsNullOrWhiteSpace(areaNodeId))
        {
            return Result.WithMessages<LevelDossier>(
                ValidationMessage.Error("An area node id is required.", nameof(areaNodeId)));
        }

        if (levelByArea.TryGetValue(areaNodeId, out LevelDossier level))
        {
            return Result.For(level);
        }

        // The node may itself be a level node (the DM navigated to the level rather than an area).
        if (levelsByNode.TryGetValue(areaNodeId, out LevelDossier ownLevel))
        {
            return Result.For(ownLevel);
        }

        return Result.WithMessages<LevelDossier>(
            ValidationMessage.Error($"No level dossier is authored for area '{areaNodeId}'.", nameof(areaNodeId)));
    }

    /// <inheritdoc />
    public IReadOnlyList<AreaDossier> AllAreas() => allAreas;

    /// <inheritdoc />
    public IReadOnlyList<LevelDossier> AllLevels() => allLevels;

    /// <inheritdoc />
    public Result<int> GetLevelNumber(string levelNodeId)
    {
        if (string.IsNullOrWhiteSpace(levelNodeId))
        {
            return Result.WithMessages<int>(
                ValidationMessage.Error("A level node id is required.", nameof(levelNodeId)));
        }

        return levelNumbers.TryGetValue(levelNodeId, out int number)
            ? Result.For(number)
            : Result.WithMessages<int>(
                ValidationMessage.Error($"Level '{levelNodeId}' has no floor number: it is not authored, or its file is not named level-<n>.json.", nameof(levelNodeId)));
    }

    /// <inheritdoc />
    public Result<ReferenceLibrary> GetReference() =>
        hasReference
            ? Result.For(reference)
            : Result.WithMessages<ReferenceLibrary>(
                ValidationMessage.Error("No rules reference is authored.", "reference"));

    /// <inheritdoc />
    public Result<CampaignDossier> GetCampaign() =>
        hasCampaign
            ? Result.For(campaign)
            : Result.WithMessages<CampaignDossier>(
                ValidationMessage.Error("No campaign dossier is authored.", "campaign"));

    /// <inheritdoc />
    public Result<PartyDossier> GetParty() =>
        hasParty
            ? Result.For(party)
            : Result.WithMessages<PartyDossier>(
                ValidationMessage.Error("No party dossier is authored.", "party"));

    /// <inheritdoc />
    public Result<NpcRoster> GetNpcs() =>
        hasNpcs
            ? Result.For(npcs)
            : Result.WithMessages<NpcRoster>(
                ValidationMessage.Error("No NPC roster is authored.", "npcs"));

    /// <inheritdoc />
    public Result<StoryLibrary> GetStory() =>
        hasStory
            ? Result.For(story)
            : Result.WithMessages<StoryLibrary>(
                ValidationMessage.Error("No prior-campaign story is authored.", "story"));

    /// <inheritdoc />
    public Result<SessionLog> GetSessions() =>
        hasSessions
            ? Result.For(sessions)
            : Result.WithMessages<SessionLog>(
                ValidationMessage.Error("No session prep is authored.", "sessions"));

    /// <inheritdoc />
    public Result<RelationList> GetRelations() =>
        hasRelations
            ? Result.For(relations)
            : Result.WithMessages<RelationList>(
                ValidationMessage.Error("No campaign-wide relations are authored.", "relations"));

    /// <inheritdoc />
    public IReadOnlyList<CardDeck> AllDecks() => decks;

    /// <inheritdoc />
    public Result<QuestLog> GetQuests() =>
        hasQuests
            ? Result.For(quests)
            : Result.WithMessages<QuestLog>(
                ValidationMessage.Error("No quest log is authored.", "quests"));

    // Every matching document, in a STABLE order. Both merge loops resolve a duplicate id by
    // letting the last document win, so which document is "last" has to be something better than
    // whatever the file system happened to hand back — Directory.EnumerateFiles explicitly does not
    // guarantee an order, so without this the winner could differ between two machines reading
    // identical files.
    //
    // Ordinal by file name, which is worth reading carefully before naming a second document:
    // '-' (0x2D) sorts BEFORE '.' (0x2E), so "quests-side.json" loads *before* "quests.json" and
    // loses to it, while "quests_side.json" ('_' is 0x5F) loads after and wins. Deterministic
    // either way, which is the point; the precedence just isn't the one a glance would assume.
    private static IEnumerable<string> Documents(string dossiersRoot, string pattern) =>
        Directory
            .EnumerateFiles(dossiersRoot, pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal);

    // Last document wins on a duplicate id, matching how the level files index areas, while keeping
    // the authored order of the first appearance so the quest log reads in book order and the decks
    // in file order. Each id written twice is reported against the file of the one that wins.
    private List<T> Dedupe<T>(List<(T Item, string File)> merged, Func<T, string> idOf, string what)
    {
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = new List<string>(merged.Count);
        var ordered = new List<T>(merged.Count);
        foreach ((T item, string file) in merged)
        {
            string id = idOf(item);
            if (byId.TryGetValue(id, out int at))
            {
                Report(file, $"the {what} '{id}' is also written in {Path.GetFileName(files[at])}; the app keeps this one, the later.");
                ordered[at] = item;
                files[at] = file;
            }
            else
            {
                byId[id] = ordered.Count;
                ordered.Add(item);
                files.Add(file);
            }
        }

        return ordered;
    }

    private void Report(string file, string problem) => problems.Add(new ContentProblem(file, problem));

    // A level file whose "level" is null is read as a level with no id, the way one that writes {} or
    // leaves "level" out already is, rather than skipped: its areas are indexed on no floor. GetArea and
    // search still find them and GetLevelForArea hands back that empty level, but no [[area …]] link
    // can name them, since a printed key only means something on a floor. The content tests name the
    // file ("declares no levelNodeId").
    private void IndexLevelFile(string path)
    {
        if (!TryRead<LevelDossierSet>(path, out LevelDossierSet set))
        {
            return;
        }

        LevelDossier level = set.Level ?? new LevelDossier();
        if (!string.IsNullOrEmpty(level.LevelNodeId))
        {
            if (levelFiles.TryGetValue(level.LevelNodeId, out string first))
            {
                Report(path, $"the level '{level.LevelNodeId}' is also declared in {Path.GetFileName(first)}; the app keeps this one, the later.");
            }

            levelFiles[level.LevelNodeId] = path;
            levelsByNode[level.LevelNodeId] = level;
            if (TryFloorNumber(path, out int number))
            {
                levelNumbers[level.LevelNodeId] = number;
            }
        }

        for (int i = 0; i < set.Areas.Count; i++)
        {
            AreaDossier area = set.Areas[i];
            if (string.IsNullOrEmpty(area.AreaNodeId))
            {
                Report(path, $"areas[{i}] ('{area.Title}') has no areaNodeId, so the app dropped it.");
                continue;
            }

            if (areaFiles.TryGetValue(area.AreaNodeId, out string earlier))
            {
                Report(path, $"the area '{area.AreaNodeId}' is also written in {Path.GetFileName(earlier)}; the app keeps this one, the later.");
            }

            areaFiles[area.AreaNodeId] = path;
            areasByNode[area.AreaNodeId] = area;
            levelByArea[area.AreaNodeId] = level;
        }
    }

    // The n of "level-n.json"; a level file named any other way still loads, it just has no number
    // for another floor's prose to reach it by. Internal so the content tests judge a file name the
    // same way.
    internal static bool TryFloorNumber(string path, out int number)
    {
        const string Prefix = "level-";
        string name = Path.GetFileNameWithoutExtension(path);
        number = 0;
        return name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            && name.Length > Prefix.Length
            && name[Prefix.Length..].All(char.IsAsciiDigit)
            && int.TryParse(name[Prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out number)
            && number > 0;
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
}
