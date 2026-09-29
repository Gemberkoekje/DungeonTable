using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Stats;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// In-memory stand-ins for the stores behind <see cref="DungeonTable.Web.Services.DmWorkspace"/>,
/// so the workspace's own behaviour (map loading, navigation, room/region resolution) can be tested
/// without the real content, maps or dossier files.
/// </summary>
internal sealed class FakeContentProjection : IContentProjection
{
    /// <summary>Briefings keyed by node id; a missing key yields an invalid result.</summary>
    public Dictionary<string, RoomBriefing> Briefings { get; } =
        new Dictionary<string, RoomBriefing>(StringComparer.Ordinal);

    /// <summary>Reference cards keyed by node id; a missing key yields an invalid result.</summary>
    public Dictionary<string, ReferenceCard> Cards { get; } =
        new Dictionary<string, ReferenceCard>(StringComparer.Ordinal);

    /// <summary>The matches every search returns, whatever the query.</summary>
    public List<SearchMatch> Matches { get; } = new List<SearchMatch>();

    public Result<RoomBriefing> GetBriefing(string nodeId) =>
        Briefings.TryGetValue(nodeId, out RoomBriefing briefing)
            ? Result.For(briefing)
            : Result.WithMessages<RoomBriefing>(ValidationMessage.Error($"No node '{nodeId}'.", nameof(nodeId)));

    public IReadOnlyList<SearchMatch> SearchNodes(string query, int limit) => Matches;

    public Result<ReferenceCard> GetReferenceCard(string nodeId) =>
        Cards.TryGetValue(nodeId, out ReferenceCard card)
            ? Result.For(card)
            : Result.WithMessages<ReferenceCard>(ValidationMessage.Error($"No node '{nodeId}'.", nameof(nodeId)));

    /// <summary>Relation lines keyed by the end they are read from; a missing key yields none.</summary>
    public Dictionary<string, IReadOnlyList<RelationLine>> Relations { get; } =
        new Dictionary<string, IReadOnlyList<RelationLine>>(StringComparer.Ordinal);

    public IReadOnlyList<RelationLine> RelationsOf(string id) =>
        Relations.TryGetValue(id ?? string.Empty, out IReadOnlyList<RelationLine> lines) ? lines : Array.Empty<RelationLine>();

    /// <summary>The stat block each individual fights with, keyed by its id; a missing key means none.</summary>
    public Dictionary<string, string> StatBlocks { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public string StatBlockOf(string id) =>
        StatBlocks.TryGetValue(id ?? string.Empty, out string block) ? block : string.Empty;

    /// <summary>What the book index says, keyed by id; a missing key yields an invalid result.</summary>
    public Dictionary<string, ReferenceCard> BookCards { get; } =
        new Dictionary<string, ReferenceCard>(StringComparer.Ordinal);

    public Result<ReferenceCard> GetBookCard(string id) =>
        BookCards.TryGetValue(id ?? string.Empty, out ReferenceCard card)
            ? Result.For(card)
            : Result.WithMessages<ReferenceCard>(ValidationMessage.Error($"No book entry '{id}'.", nameof(id)));
}

/// <summary>A resolver that finds the cross-references it has been given, whatever the text.</summary>
internal sealed class FakeCrossRefResolver : ICrossRefResolver
{
    /// <summary>The references every <see cref="Resolve(string)"/> call returns; empty by default.</summary>
    public List<CrossRef> Refs { get; } = new List<CrossRef>();

    /// <summary>
    /// The floor node id each call was scoped to, in call order. The real resolver reads areas out
    /// of one floor's bucket, so a caller passing the wrong floor (or none) is a real defect that
    /// only shows up as a missing or wrong-floor link — recording it here makes it assertable.
    /// </summary>
    public List<string> ScopedTo { get; } = new List<string>();

    public IReadOnlyList<CrossRef> Resolve(string text) => Resolve(text, string.Empty);

    public IReadOnlyList<CrossRef> Resolve(string text, string levelNodeId)
    {
        ScopedTo.Add(levelNodeId ?? string.Empty);
        return Refs.ToArray();
    }
}

/// <summary>A vector-map store serving maps held in memory.</summary>
internal sealed class FakeVectorMapStore : IVectorMapStore
{
    public Dictionary<string, VectorMap> Maps { get; } = new Dictionary<string, VectorMap>(StringComparer.Ordinal);

    public IReadOnlyList<string> ListMapIds() => Maps.Keys.ToArray();

    public Task<Result<VectorMap>> LoadAsync(string mapId, CancellationToken cancellationToken) =>
        Task.FromResult(Maps.TryGetValue(mapId, out VectorMap map)
            ? Result.For(map)
            : Result.WithMessages<VectorMap>(ValidationMessage.Error($"No map '{mapId}'.", nameof(mapId))));
}

/// <summary>An annotation store serving map definitions held in memory.</summary>
internal sealed class FakeMapStore : IMapStore
{
    public Dictionary<string, MapDefinition> Definitions { get; } =
        new Dictionary<string, MapDefinition>(StringComparer.Ordinal);

    public IReadOnlyList<string> ListMapIds() => Definitions.Keys.ToArray();

    public bool Exists(string mapId) => Definitions.ContainsKey(mapId);

    public Task<Result<MapDefinition>> LoadAsync(string mapId, CancellationToken cancellationToken) =>
        Task.FromResult(Definitions.TryGetValue(mapId, out MapDefinition definition)
            ? Result.For(definition)
            : Result.WithMessages<MapDefinition>(ValidationMessage.Error($"No notes for '{mapId}'.", nameof(mapId))));

    public Task<Result> SaveAsync(MapDefinition map, CancellationToken cancellationToken)
    {
        Definitions[map.MapId] = map;
        return Task.FromResult(Result.OK);
    }
}

/// <summary>A dossier store serving area dossiers held in memory.</summary>
internal sealed class FakeDossierStore : IDossierStore
{
    public Dictionary<string, AreaDossier> Areas { get; } = new Dictionary<string, AreaDossier>(StringComparer.Ordinal);

    public Result<AreaDossier> GetArea(string areaNodeId) =>
        Areas.TryGetValue(areaNodeId, out AreaDossier area)
            ? Result.For(area)
            : Result.WithMessages<AreaDossier>(ValidationMessage.Error($"No dossier for '{areaNodeId}'.", nameof(areaNodeId)));

    /// <summary>The floor each area sits on, keyed by area node id; an absent key means no floor.</summary>
    public Dictionary<string, LevelDossier> LevelsByArea { get; } =
        new Dictionary<string, LevelDossier>(StringComparer.Ordinal);

    public Result<LevelDossier> GetLevel(string levelNodeId) =>
        Result.WithMessages<LevelDossier>(ValidationMessage.Error("No levels.", nameof(levelNodeId)));

    public Result<LevelDossier> GetLevelForArea(string areaNodeId) =>
        LevelsByArea.TryGetValue(areaNodeId ?? string.Empty, out LevelDossier level)
            ? Result.For(level)
            : Result.WithMessages<LevelDossier>(ValidationMessage.Error("No levels.", nameof(areaNodeId)));

    public IReadOnlyList<AreaDossier> AllAreas() => Areas.Values.ToArray();

    /// <summary>Floors every <see cref="AllLevels"/> call returns, beside those reached per area.</summary>
    public List<LevelDossier> Levels { get; } = new List<LevelDossier>();

    public IReadOnlyList<LevelDossier> AllLevels() =>
        Levels.Concat(LevelsByArea.Values).Distinct().ToArray();

    /// <summary>Each floor's number, keyed by level node id; an absent key means the level has none.</summary>
    public Dictionary<string, int> LevelNumbers { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

    public Result<int> GetLevelNumber(string levelNodeId) =>
        LevelNumbers.TryGetValue(levelNodeId ?? string.Empty, out int number)
            ? Result.For(number)
            : Result.WithMessages<int>(ValidationMessage.Error("No number.", nameof(levelNodeId)));

    public Result<ReferenceLibrary> GetReference() => Result.For(new ReferenceLibrary());

    /// <summary>The quest log every <see cref="GetQuests"/> call returns.</summary>
    public QuestLog Quests { get; set; } = new QuestLog();

    public Result<QuestLog> GetQuests() => Result.For(Quests);

    /// <summary>The campaign dossier <see cref="GetCampaign"/> serves while <see cref="HasCampaign"/> is set.</summary>
    public CampaignDossier Campaign { get; set; } = new CampaignDossier();

    /// <summary>
    /// When false, <see cref="GetCampaign"/> reports "not authored" the way the real store does for
    /// a table with no <c>campaign.json</c> — so a caller that reads <c>.Value</c> without checking
    /// <c>IsValid</c> fails a test instead of only failing in front of a DM.
    /// </summary>
    public bool HasCampaign { get; set; } = true;

    public Result<CampaignDossier> GetCampaign() => HasCampaign
        ? Result.For(Campaign)
        : Result.WithMessages<CampaignDossier>(ValidationMessage.Error("No campaign.", "campaign"));

    /// <summary>The party dossier <see cref="GetParty"/> serves while <see cref="HasParty"/> is set.</summary>
    public PartyDossier Party { get; set; } = new PartyDossier();

    /// <summary>When false, <see cref="GetParty"/> reports "not authored", as for a table with no <c>characters.json</c>.</summary>
    public bool HasParty { get; set; } = true;

    public Result<PartyDossier> GetParty() => HasParty
        ? Result.For(Party)
        : Result.WithMessages<PartyDossier>(ValidationMessage.Error("No party.", "party"));

    /// <summary>The NPC roster <see cref="GetNpcs"/> serves while <see cref="HasNpcs"/> is set.</summary>
    public NpcRoster Npcs { get; set; } = new NpcRoster();

    /// <summary>When false, <see cref="GetNpcs"/> reports "not authored", as for a table with no <c>npcs.json</c>.</summary>
    public bool HasNpcs { get; set; } = true;

    public Result<NpcRoster> GetNpcs() => HasNpcs
        ? Result.For(Npcs)
        : Result.WithMessages<NpcRoster>(ValidationMessage.Error("No NPCs.", "npcs"));

    /// <summary>The prior-campaign history <see cref="GetStory"/> serves while <see cref="HasStory"/> is set.</summary>
    public StoryLibrary Story { get; set; } = new StoryLibrary();

    /// <summary>When false, <see cref="GetStory"/> reports "not authored", as for a table starting fresh.</summary>
    public bool HasStory { get; set; } = true;

    public Result<StoryLibrary> GetStory() => HasStory
        ? Result.For(Story)
        : Result.WithMessages<StoryLibrary>(ValidationMessage.Error("No story.", "story"));

    /// <summary>The session prep <see cref="GetSessions"/> serves while <see cref="HasSessions"/> is set.</summary>
    public SessionLog Sessions { get; set; } = new SessionLog();

    /// <summary>When false, <see cref="GetSessions"/> reports "not authored", as for a table with no prep.</summary>
    public bool HasSessions { get; set; } = true;

    public Result<SessionLog> GetSessions() => HasSessions
        ? Result.For(Sessions)
        : Result.WithMessages<SessionLog>(ValidationMessage.Error("No sessions.", "sessions"));

    /// <summary>The campaign-wide relations <see cref="GetRelations"/> serves while <see cref="HasRelations"/> is set.</summary>
    public RelationList Relations { get; set; } = new RelationList();

    /// <summary>When false, <see cref="GetRelations"/> reports "not authored", as for a table with no <c>relations.json</c>.</summary>
    public bool HasRelations { get; set; } = true;

    public Result<RelationList> GetRelations() => HasRelations
        ? Result.For(Relations)
        : Result.WithMessages<RelationList>(ValidationMessage.Error("No relations.", "relations"));

    /// <summary>The decks every <see cref="AllDecks"/> call returns, in order; none by default.</summary>
    public List<CardDeck> Decks { get; } = new List<CardDeck>();

    public IReadOnlyList<CardDeck> AllDecks() => Decks.ToArray();
}

/// <summary>A stat library serving monster/spell entries held in memory.</summary>
internal sealed class FakeStatLibrary : IStatLibrary
{
    public Dictionary<string, StatBlock> Monsters { get; } = new Dictionary<string, StatBlock>(StringComparer.Ordinal);

    public Dictionary<string, SpellEntry> Spells { get; } = new Dictionary<string, SpellEntry>(StringComparer.Ordinal);

    public Result<StatBlock> GetMonster(string nodeId) =>
        Monsters.TryGetValue(nodeId, out StatBlock monster)
            ? Result.For(monster)
            : Result.WithMessages<StatBlock>(ValidationMessage.Error($"No stat block for '{nodeId}'.", nameof(nodeId)));

    public Result<SpellEntry> GetSpell(string nodeId) =>
        Spells.TryGetValue(nodeId, out SpellEntry spell)
            ? Result.For(spell)
            : Result.WithMessages<SpellEntry>(ValidationMessage.Error($"No spell entry for '{nodeId}'.", nameof(nodeId)));

    public bool HasMonster(string nodeId) => Monsters.ContainsKey(nodeId);

    public IReadOnlyList<StatBlock> AllMonsters() => Monsters.Values.ToArray();

    public IReadOnlyList<SpellEntry> AllSpells() => Spells.Values.ToArray();

    public IReadOnlyList<SearchMatch> SearchMonsters(string query, int limit) => Array.Empty<SearchMatch>();
}
