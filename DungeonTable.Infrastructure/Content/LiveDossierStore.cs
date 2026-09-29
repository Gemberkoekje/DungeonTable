using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Dossiers;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// The <see cref="IDossierStore"/> the pages see: every call is answered by the dossiers of the
/// content showing now (<see cref="LiveContent.Current"/>), so a reload reaches every page without
/// any of them holding a new store.
/// </summary>
public sealed class LiveDossierStore : IDossierStore
{
    private readonly LiveContent live;

    /// <summary>Creates the store over the live content.</summary>
    /// <param name="live">The live content.</param>
    public LiveDossierStore(LiveContent live)
    {
        ArgumentNullException.ThrowIfNull(live);
        this.live = live;
    }

    private FileSystemDossierStore Now => live.Current.Dossiers;

    /// <inheritdoc />
    public Result<AreaDossier> GetArea(string areaNodeId) => Now.GetArea(areaNodeId);

    /// <inheritdoc />
    public Result<LevelDossier> GetLevel(string levelNodeId) => Now.GetLevel(levelNodeId);

    /// <inheritdoc />
    public Result<LevelDossier> GetLevelForArea(string areaNodeId) => Now.GetLevelForArea(areaNodeId);

    /// <inheritdoc />
    public IReadOnlyList<AreaDossier> AllAreas() => Now.AllAreas();

    /// <inheritdoc />
    public IReadOnlyList<LevelDossier> AllLevels() => Now.AllLevels();

    /// <inheritdoc />
    public Result<int> GetLevelNumber(string levelNodeId) => Now.GetLevelNumber(levelNodeId);

    /// <inheritdoc />
    public Result<ReferenceLibrary> GetReference() => Now.GetReference();

    /// <inheritdoc />
    public Result<QuestLog> GetQuests() => Now.GetQuests();

    /// <inheritdoc />
    public Result<CampaignDossier> GetCampaign() => Now.GetCampaign();

    /// <inheritdoc />
    public Result<PartyDossier> GetParty() => Now.GetParty();

    /// <inheritdoc />
    public Result<NpcRoster> GetNpcs() => Now.GetNpcs();

    /// <inheritdoc />
    public Result<StoryLibrary> GetStory() => Now.GetStory();

    /// <inheritdoc />
    public Result<SessionLog> GetSessions() => Now.GetSessions();

    /// <inheritdoc />
    public Result<RelationList> GetRelations() => Now.GetRelations();

    /// <inheritdoc />
    public IReadOnlyList<CardDeck> AllDecks() => Now.AllDecks();
}
