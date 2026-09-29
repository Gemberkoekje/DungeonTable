using DungeonTable.Core.Dossier;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Serves the authored dossiers (per-area, per-level, the shared rules reference, and the
/// campaign-wide documents) that the room briefings, cards and links are built from. Implemented by
/// the file-system adapter that reads the <c>data/dossiers/*.json</c> documents at startup, and
/// again, whole, whenever one of them changes on disk.
/// </summary>
public interface IDossierStore
{
    /// <summary>Gets the dossier for a keyed area by its node id.</summary>
    /// <param name="areaNodeId">The area's node id ("data_dossiers_level_1_area_1").</param>
    /// <returns>The area dossier, or an invalid result when none is authored for the node.</returns>
    Result<AreaDossier> GetArea(string areaNodeId);

    /// <summary>Gets the floor-level dossier for a level by its node id.</summary>
    /// <param name="levelNodeId">The level's node id ("data_dossiers_level_1").</param>
    /// <returns>The level dossier, or an invalid result when none is authored for the node.</returns>
    Result<LevelDossier> GetLevel(string levelNodeId);

    /// <summary>
    /// Gets the floor-level dossier that a keyed area belongs to, so the DM panel can show the
    /// current floor's overview alongside the room. A level node id resolves to its own dossier.
    /// </summary>
    /// <param name="areaNodeId">A keyed area's node id ("data_dossiers_level_1_area_1") or a level node id.</param>
    /// <returns>The owning level dossier, or an invalid result when the node maps to no authored level.</returns>
    Result<LevelDossier> GetLevelForArea(string areaNodeId);

    /// <summary>
    /// Every authored area, across every level. Unlike <see cref="GetArea"/> this is for callers
    /// that index the whole set rather than look one up: the link index, which resolves an
    /// <c>[[area 19]]</c> to the area it names, and search.
    /// </summary>
    /// <returns>The authored areas, in load order (possibly empty).</returns>
    IReadOnlyList<AreaDossier> AllAreas();

    /// <summary>
    /// Every authored floor-level dossier, across every level. The counterpart to
    /// <see cref="AllAreas"/> for callers that index the whole set rather than look one up. A
    /// level's factions and wandering monsters are authored here rather than in any one room, so
    /// the reference-card fallback needs the whole set to describe them.
    /// </summary>
    /// <returns>The authored levels, in load order (possibly empty).</returns>
    IReadOnlyList<LevelDossier> AllLevels();

    /// <summary>
    /// Gets the floor number of an authored level: the <c>n</c> of the <c>level-{n}.json</c> it was
    /// loaded from. It is how prose on one floor names an area on another (<c>[[L2 area 14]]</c>), so
    /// it comes from the file name every level already has to follow rather than from a title a DM
    /// might reword.
    /// </summary>
    /// <param name="levelNodeId">The level's node id ("data_dossiers_level_1").</param>
    /// <returns>
    /// The floor number, or an invalid result when no such level is authored or its file name
    /// carries no number.
    /// </returns>
    Result<int> GetLevelNumber(string levelNodeId);

    /// <summary>Gets the shared, level-independent rules reference (doors, light, resting).</summary>
    /// <returns>The reference library, or an invalid result when none is authored.</returns>
    Result<ReferenceLibrary> GetReference();

    /// <summary>
    /// Gets the campaign-wide quest log: the book's adventure hooks with their story-beat chains.
    /// Campaign-scoped rather than per-level — a quest given in the village above is completed
    /// levels further down.
    /// </summary>
    /// <returns>The quest log, or an invalid result when none is authored.</returns>
    Result<QuestLog> GetQuests();

    /// <summary>
    /// Gets the campaign-wide background dossier: the book's front matter that belongs to no single
    /// level (the place's history, what makes it strange, the way in, and the dungeon-wide level
    /// table).
    /// </summary>
    /// <returns>The campaign dossier, or an invalid result when none is authored.</returns>
    Result<CampaignDossier> GetCampaign();

    /// <summary>
    /// Gets the party dossier: the player characters the dungeon is being run for, with the numbers
    /// the DM calls for at the table and the threads their players wrote. Campaign-scoped, like the
    /// quest log.
    /// </summary>
    /// <returns>The party dossier, or an invalid result when none is authored.</returns>
    Result<PartyDossier> GetParty();

    /// <summary>
    /// Gets the campaign's named non-player characters: who they are, where to find them, and what
    /// they can tell the party. Campaign-scoped, like the party dossier.
    /// </summary>
    /// <returns>The NPC roster, or an invalid result when none is authored.</returns>
    Result<NpcRoster> GetNpcs();

    /// <summary>
    /// Gets the table's prior-campaign history: what the players lived through before this
    /// campaign, who they met, and what it left unfinished. Empty for a table starting fresh.
    /// </summary>
    /// <returns>The story library, or an invalid result when none is authored.</returns>
    Result<StoryLibrary> GetStory();

    /// <summary>
    /// Gets the table's session prep: the running order of an evening's scenes, the facts it rests
    /// on, and what the DM still has to decide. Campaign-scoped, like the quest log.
    /// </summary>
    /// <returns>The session log, or an invalid result when none is authored.</returns>
    Result<SessionLog> GetSessions();

    /// <summary>
    /// Gets the campaign-wide relations: the cross-connections no single floor owns (the party's
    /// contacts, threads that run between levels). A level's own relations come with its dossier.
    /// </summary>
    /// <returns>The relation list, or an invalid result when none is authored.</returns>
    Result<RelationList> GetRelations();

    /// <summary>
    /// Every card deck the campaign authors, one per <c>deck-*.json</c> in file-name order, so the DM
    /// can browse one or draw from it without reaching for the book mid-session. A deck with no id is
    /// left out; where two share an id, the later file's wins.
    /// </summary>
    /// <returns>The decks (possibly none).</returns>
    IReadOnlyList<CardDeck> AllDecks();
}
