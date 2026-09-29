using System.Collections.Generic;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>
/// A dossier store serving content held in memory, for proving a rule catches what it exists to catch.
/// A copy of the ordinary suite's fake: an xUnit v3 test project is an executable, and one cannot
/// reference another.
/// </summary>
internal sealed class FakeDossierStore : IDossierStore
{
    public Dictionary<string, AreaDossier> Areas { get; } = new Dictionary<string, AreaDossier>(StringComparer.Ordinal);

    /// <summary>The floor each area sits on, keyed by area node id; an absent key means no floor.</summary>
    public Dictionary<string, LevelDossier> LevelsByArea { get; } = new Dictionary<string, LevelDossier>(StringComparer.Ordinal);

    /// <summary>Floors every <see cref="AllLevels"/> call returns, beside those reached per area.</summary>
    public List<LevelDossier> Levels { get; } = new List<LevelDossier>();

    /// <summary>Each floor's number, keyed by level node id; an absent key means the level has none.</summary>
    public Dictionary<string, int> LevelNumbers { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

    public QuestLog Quests { get; set; } = new QuestLog();

    public CampaignDossier Campaign { get; set; } = new CampaignDossier();

    public PartyDossier Party { get; set; } = new PartyDossier();

    public NpcRoster Npcs { get; set; } = new NpcRoster();

    public StoryLibrary Story { get; set; } = new StoryLibrary();

    public SessionLog Sessions { get; set; } = new SessionLog();

    public RelationList Relations { get; set; } = new RelationList();

    public List<CardDeck> Decks { get; } = new List<CardDeck>();

    public Result<AreaDossier> GetArea(string areaNodeId) =>
        Areas.TryGetValue(areaNodeId ?? string.Empty, out AreaDossier area)
            ? Result.For(area)
            : Result.WithMessages<AreaDossier>(ValidationMessage.Error($"No dossier for '{areaNodeId}'.", nameof(areaNodeId)));

    public Result<LevelDossier> GetLevel(string levelNodeId) =>
        AllLevels().FirstOrDefault(level => string.Equals(level.LevelNodeId, levelNodeId, StringComparison.Ordinal)) is { } found
            ? Result.For(found)
            : Result.WithMessages<LevelDossier>(ValidationMessage.Error("No such level.", nameof(levelNodeId)));

    public Result<LevelDossier> GetLevelForArea(string areaNodeId) =>
        LevelsByArea.TryGetValue(areaNodeId ?? string.Empty, out LevelDossier level)
            ? Result.For(level)
            : Result.WithMessages<LevelDossier>(ValidationMessage.Error("No levels.", nameof(areaNodeId)));

    public IReadOnlyList<AreaDossier> AllAreas() => Areas.Values.ToArray();

    public IReadOnlyList<LevelDossier> AllLevels() => Levels.Concat(LevelsByArea.Values).Distinct().ToArray();

    public Result<int> GetLevelNumber(string levelNodeId) =>
        LevelNumbers.TryGetValue(levelNodeId ?? string.Empty, out int number)
            ? Result.For(number)
            : Result.WithMessages<int>(ValidationMessage.Error("No number.", nameof(levelNodeId)));

    public Result<ReferenceLibrary> GetReference() => Result.For(new ReferenceLibrary());

    public Result<QuestLog> GetQuests() => Result.For(Quests);

    public Result<CampaignDossier> GetCampaign() => Result.For(Campaign);

    public Result<PartyDossier> GetParty() => Result.For(Party);

    public Result<NpcRoster> GetNpcs() => Result.For(Npcs);

    public Result<StoryLibrary> GetStory() => Result.For(Story);

    public Result<SessionLog> GetSessions() => Result.For(Sessions);

    public Result<RelationList> GetRelations() => Result.For(Relations);

    public IReadOnlyList<CardDeck> AllDecks() => Decks.ToArray();
}

/// <summary>A stat library serving stat blocks and spells held in memory.</summary>
internal sealed class FakeStatLibrary : IStatLibrary
{
    public Dictionary<string, StatBlock> Monsters { get; } = new Dictionary<string, StatBlock>(StringComparer.Ordinal);

    public Dictionary<string, SpellEntry> Spells { get; } = new Dictionary<string, SpellEntry>(StringComparer.Ordinal);

    public Result<StatBlock> GetMonster(string nodeId) =>
        Monsters.TryGetValue(nodeId ?? string.Empty, out StatBlock monster)
            ? Result.For(monster)
            : Result.WithMessages<StatBlock>(ValidationMessage.Error($"No stat block for '{nodeId}'.", nameof(nodeId)));

    public Result<SpellEntry> GetSpell(string nodeId) =>
        Spells.TryGetValue(nodeId ?? string.Empty, out SpellEntry spell)
            ? Result.For(spell)
            : Result.WithMessages<SpellEntry>(ValidationMessage.Error($"No spell entry for '{nodeId}'.", nameof(nodeId)));

    public bool HasMonster(string nodeId) => Monsters.ContainsKey(nodeId ?? string.Empty);

    public IReadOnlyList<StatBlock> AllMonsters() => Monsters.Values.ToArray();

    public IReadOnlyList<SpellEntry> AllSpells() => Spells.Values.ToArray();

    public IReadOnlyList<SearchMatch> SearchMonsters(string query, int limit) => Array.Empty<SearchMatch>();
}
