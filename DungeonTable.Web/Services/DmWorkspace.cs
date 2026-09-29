using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Content;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Web.Services;

/// <summary>
/// Everything the DM screen's three tabs share: the loaded vector map and its editor annotations,
/// the room briefing currently on show, and the reference history that threads them together.
/// Registered <b>Scoped</b>, so there is one per Blazor circuit (one DM browser) — unlike
/// <see cref="SessionState"/>, which is the singleton the player projector also observes. Splitting
/// this out of the DM page keeps the Map / Info / Battle tabs from prop-drilling a dozen parameters
/// each, and lets a tab mutate shared state without knowing who else is watching:
/// <see cref="Changed"/> tells the shell to re-render all of them.
/// </summary>
public sealed class DmWorkspace
{
    // The Search tab shows at most this many matches per query.
    private const int SearchLimit = 20;

    private readonly IContentProjection content;
    private readonly ICrossRefResolver crossRefs;
    private readonly IVectorMapStore vectorMaps;
    private readonly IMapStore annotations;
    private readonly IDossierStore dossiers;
    private readonly IStatLibrary stats;
    private readonly EncounterSource encounters;
    private readonly SessionState session;

    private readonly NavHistory history = new NavHistory();
    private readonly List<Region> regions = new List<Region>();
    private readonly List<FeatureMarker> markers = new List<FeatureMarker>();
    private readonly List<ObjectAnnotation> objects = new List<ObjectAnnotation>();
    private readonly HashSet<string> secretDoorIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> doubleDoorIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> defaultOpenDoorIds = new HashSet<string>(StringComparer.Ordinal);
    // Objects (stairs / decorations) the Room Editor marked concealed. Each starts hidden from the
    // players and is toggled for the session in Conceal mode, through the shared reveal set.
    private readonly HashSet<string> concealedObjectIds = new HashSet<string>(StringComparer.Ordinal);
    // Region ids on the current map linked to the shown room, used by the "reveal room" control (a
    // room may be split into several regions across corridors, so this is a list).
    private readonly List<string> roomRegionIds = new List<string>();

    // The level name the Room Editor gave each map, which the map picker lists it under.
    private IReadOnlyDictionary<string, string> levelNames = new Dictionary<string, string>(StringComparer.Ordinal);

    private bool initialised;

    /// <summary>Creates the workspace over the stores that back the DM screen.</summary>
    /// <param name="content">The content projection (briefings, reference cards, search).</param>
    /// <param name="crossRefs">The resolver that turns dossier prose into clickable links.</param>
    /// <param name="vectorMaps">The vector-map store (the rendered geometry).</param>
    /// <param name="annotations">The annotation store (regions, markers, object state).</param>
    /// <param name="dossiers">The prose dossier store (areas, levels, rules reference).</param>
    /// <param name="stats">The extracted monster/spell stat-block library.</param>
    /// <param name="encounters">Works out the shown area's encounter from the creatures it lists.</param>
    /// <param name="session">The shared DM/player reveal state.</param>
    public DmWorkspace(
        IContentProjection content,
        ICrossRefResolver crossRefs,
        IVectorMapStore vectorMaps,
        IMapStore annotations,
        IDossierStore dossiers,
        IStatLibrary stats,
        EncounterSource encounters,
        SessionState session)
    {
        this.content = content;
        this.crossRefs = crossRefs;
        this.vectorMaps = vectorMaps;
        this.annotations = annotations;
        this.dossiers = dossiers;
        this.stats = stats;
        this.encounters = encounters;
        this.session = session;
    }

    /// <summary>Raised whenever any shared DM state changes, so the shell can re-render every tab.</summary>
    public event Action Changed;

    // ---- Map ------------------------------------------------------------------------------

    /// <summary>The ids of every vector map available to load.</summary>
    public IReadOnlyList<string> MapIds { get; private set; } = Array.Empty<string>();

    /// <summary>The id of the loaded map.</summary>
    public string CurrentMapId { get; private set; } = string.Empty;

    /// <summary>What the map picker calls a map: the level name set in the Room Editor, or its id.</summary>
    /// <param name="mapId">One of <see cref="MapIds"/>.</param>
    /// <returns>The map's label.</returns>
    public string MapLabel(string mapId) => MapLabels.For(levelNames, mapId);

    /// <summary>The loaded vector map; an empty map when none loaded.</summary>
    public VectorMap Map { get; private set; } = new VectorMap();

    /// <summary>True once a map has loaded successfully.</summary>
    public bool MapLoaded { get; private set; }

    /// <summary>Why the map failed to load, or an empty string.</summary>
    public string MapError { get; private set; } = string.Empty;

    /// <summary>A warning shown when the map's annotation document exists but did not parse.</summary>
    public string AnnotationsWarning { get; private set; } = string.Empty;

    /// <summary>The map's regions, in load order.</summary>
    public IReadOnlyList<Region> Regions => regions;

    /// <summary>The map's feature markers.</summary>
    public IReadOnlyList<FeatureMarker> Markers => markers;

    /// <summary>The map's object annotations (doors and stairs).</summary>
    public IReadOnlyList<ObjectAnnotation> Objects => objects;

    /// <summary>Ids of doors marked secret; each renders as wall until discovered.</summary>
    public IReadOnlyCollection<string> SecretDoorIds => secretDoorIds;

    /// <summary>Ids of doors drawn as a pair of leaves.</summary>
    public IReadOnlyCollection<string> DoubleDoorIds => doubleDoorIds;

    /// <summary>Ids of stairs and decorations the editor marked concealed.</summary>
    public IReadOnlyCollection<string> ConcealedObjectIds => concealedObjectIds;

    /// <summary>Glyph and stroke scale in world units — the grid cell size, with a sane fallback.</summary>
    public double UnitSize
    {
        get
        {
            if (Map.Grid.CellSizePx > 0)
            {
                return Map.Grid.CellSizePx;
            }

            return Map.Bounds.Width > 0 ? Map.Bounds.Width / 40 : 1;
        }
    }

    // ---- Briefing + navigation ------------------------------------------------------------

    /// <summary>The briefing for the shown room: who is in it, where it leads, and who they are involved with.</summary>
    public RoomBriefing Briefing { get; private set; } = new RoomBriefing();

    /// <summary>The prose dossier for the shown area; empty when none is authored.</summary>
    public AreaDossier AreaDossier { get; private set; } = new AreaDossier();

    /// <summary>The floor dossier for the shown area's level; empty when none is authored.</summary>
    public LevelDossier LevelDossier { get; private set; } = new LevelDossier();

    /// <summary>The shared, level-independent rules reference.</summary>
    public ReferenceLibrary ReferenceLibrary { get; private set; } = new ReferenceLibrary();

    /// <summary>
    /// The campaign's quest log (the book's adventure hooks). Campaign-scoped, not per-room, so it
    /// is loaded once beside the rules reference rather than on every navigation.
    /// </summary>
    public QuestLog QuestLog { get; private set; } = new QuestLog();

    /// <summary>
    /// The campaign-wide background dossier (the place's history, the way in, the dungeon-wide level
    /// table), shown at the foot of the Level tab.
    /// </summary>
    public CampaignDossier CampaignDossier { get; private set; } = new CampaignDossier();

    /// <summary>
    /// The party dossier: the player characters the dungeon is being run for. Campaign-scoped like
    /// the quest log, so it is loaded once per circuit rather than on every navigation.
    /// </summary>
    public PartyDossier PartyDossier { get; private set; } = new PartyDossier();

    /// <summary>
    /// The campaign's named non-player characters, shown on the NPCs tab. Empty when none is
    /// authored.
    /// </summary>
    public NpcRoster NpcRoster { get; private set; } = new NpcRoster();

    /// <summary>
    /// The table's prior-campaign history, shown on the Story tab. Empty for a table that started
    /// fresh with this campaign.
    /// </summary>
    public StoryLibrary StoryLibrary { get; private set; } = new StoryLibrary();

    /// <summary>
    /// The table's session prep, shown on the Session tab. Empty when none is authored.
    /// </summary>
    public SessionLog SessionLog { get; private set; } = new SessionLog();

    /// <summary>
    /// The campaign's card decks, shown with a Draw button each at the foot of the Rules tab, in the
    /// order their files load.
    /// </summary>
    public IReadOnlyList<CardDeck> Decks { get; private set; } = Array.Empty<CardDeck>();

    /// <summary>Why the shown room could not be loaded, or an empty string.</summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>The node id of the room the briefing panel shows.</summary>
    public string PanelNode { get; private set; } = string.Empty;

    /// <summary>The entity whose reference card is open, or an empty string when the drawer is closed.</summary>
    public string CardNode { get; private set; } = string.Empty;

    /// <summary>The open reference card; meaningful only while <see cref="CardNode"/> is set.</summary>
    public ReferenceCard ReferenceCard { get; private set; } = new ReferenceCard();

    /// <summary>
    /// What the book index says about the open card, when the card is the campaign's own entry and the
    /// book has one under the same id (Wenna Brask's NPC card, and the book's Wenna Brask). Empty (its
    /// <see cref="ReferenceCard.NodeId"/> blank) otherwise, including when the open card is itself the
    /// book's.
    /// </summary>
    public ReferenceCard BookCard { get; private set; } = new ReferenceCard();

    /// <summary>
    /// The full stat block for the open reference card, when <see cref="CardNode"/> names a monster
    /// the stat library has extracted. Empty (its <see cref="StatBlock.NodeId"/> blank) when the
    /// card is not a monster or no block has been extracted yet — the drawer then falls back to the
    /// plain link-out summary.
    /// </summary>
    public StatBlock OpenStatBlock { get; private set; } = new StatBlock();

    /// <summary>
    /// The full spell entry for the open reference card, when <see cref="CardNode"/> names a spell
    /// the stat library has extracted. Empty (its <see cref="SpellEntry.NodeId"/> blank) when the
    /// card is not a spell or no entry has been extracted yet.
    /// </summary>
    public SpellEntry OpenSpellEntry { get; private set; } = new SpellEntry();

    /// <summary>
    /// Where <see cref="OpenStatBlock"/> is printed, by the book's title ("SRD 5.1 p.266"), as its own
    /// card cites it; empty when no block is open.
    /// </summary>
    public string OpenStatBlockCitation { get; private set; } = string.Empty;

    /// <summary>Where <see cref="OpenSpellEntry"/> is printed, by the book's title; empty when no spell is open.</summary>
    public string OpenSpellCitation { get; private set; } = string.Empty;

    /// <summary>
    /// The NPC whose card is open in the drawer, when <see cref="CardNode"/> is one of the roster's
    /// ids (an authored <c>[[wenna-brask]]</c> link). Empty (its <see cref="NpcDossier.Id"/> blank)
    /// otherwise.
    /// </summary>
    public NpcDossier OpenNpc { get; private set; } = new NpcDossier();

    /// <summary>
    /// The player character whose card is open in the drawer, when <see cref="CardNode"/> is one of
    /// the party's ids. Empty (its <see cref="CharacterDossier.Id"/> blank) otherwise.
    /// </summary>
    public CharacterDossier OpenCharacter { get; private set; } = new CharacterDossier();

    /// <summary>
    /// The creatures the shown area lists, with their authored counts. Computed once per shown area
    /// rather than per render.
    /// </summary>
    public IReadOnlyList<EncounterMonster> Encounter { get; private set; } = Array.Empty<EncounterMonster>();

    /// <summary>The reference history behind Back / Forward and the breadcrumb trail.</summary>
    public NavHistory History => history;

    /// <summary>The region highlighted on the map, or an empty string.</summary>
    public string SelectedRegionId { get; private set; } = string.Empty;

    /// <summary>A note about the last region click (e.g. "has no linked briefing yet"), or empty.</summary>
    public string RegionNotice { get; private set; } = string.Empty;

    /// <summary>Ids of the current map's regions that belong to the shown room.</summary>
    public IReadOnlyList<string> RoomRegionIds => roomRegionIds;

    /// <summary>The shown room's display title: the dossier title, else the briefing's label, else the id.</summary>
    public string PanelTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AreaDossier.Title))
            {
                return AreaDossier.Title;
            }

            if (!string.IsNullOrWhiteSpace(Briefing.Label))
            {
                return Briefing.Label;
            }

            return PanelNode;
        }
    }

    // ---- Lifecycle ------------------------------------------------------------------------

    /// <summary>
    /// Loads the rules reference, the first available map and the start area's briefing. Safe to call
    /// on every visit to the DM page: the second and later calls are no-ops, so returning to <c>/dm</c>
    /// within the same circuit keeps the loaded map, the reveal state and the history intact.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the workspace is ready.</returns>
    public async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        if (initialised)
        {
            return;
        }

        initialised = true;
        LoadCampaignDocuments();

        MapIds = vectorMaps.ListMapIds();
        levelNames = await MapLabels.LoadAsync(annotations, MapIds, cancellationToken);

        // The map the table is on, when there is one: a second DM window, or this one refreshed, must
        // not point the shared session at the first map, which clears the reveals of the one in play.
        string tableMap = session.CurrentMapId;
        string first = MapIds.Count > 0 ? MapIds[0] : string.Empty;
        await LoadMapAsync(MapIds.Contains(tableMap) ? tableMap : first, cancellationToken);
        NavigateTo(StartArea());
    }

    /// <summary>
    /// Reads again what the workspace keeps of the content, after it changed on disk: the campaign's
    /// documents and the room or card on show when the documents changed, the map list and the loaded
    /// map when a map did, or the first map when the page opened before there was one. What the table
    /// has done (reveals, the framing, open doors) is not touched, and neither is the history: the same
    /// stop is shown, read from the new content.
    /// </summary>
    /// <param name="changes">What changed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the workspace shows the new content.</returns>
    public async Task RefreshAsync(ContentChanges changes, CancellationToken cancellationToken)
    {
        if (!initialised)
        {
            return;
        }

        if (changes.HasFlag(ContentChanges.Documents))
        {
            LoadCampaignDocuments();
            if (history.Cursor >= 0)
            {
                Render();
            }
        }

        if (changes.HasFlag(ContentChanges.Maps))
        {
            MapIds = vectorMaps.ListMapIds();
            levelNames = await MapLabels.LoadAsync(annotations, MapIds, cancellationToken);
            if (MapIds.Contains(CurrentMapId))
            {
                await ReloadCurrentMapAsync(cancellationToken);
            }
            else if (CurrentMapId.Length == 0 && MapIds.Count > 0)
            {
                // The page opened before the campaign had a map, and now it has one: open it as a first
                // visit would, on the map the table is on when another DM window already chose one.
                string tableMap = session.CurrentMapId;
                await LoadMapAsync(MapIds.Contains(tableMap) ? tableMap : MapIds[0], cancellationToken);
            }
        }

        Notify();
    }

    /// <summary>
    /// Loads a vector map and its annotations, points the shared reveal state at it, and seeds the
    /// player viewport and default-open doors the first time that map is shown.
    /// </summary>
    /// <param name="mapId">The map id to load; an empty id clears the map.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the map and its annotations are loaded.</returns>
    public async Task LoadMapAsync(string mapId, CancellationToken cancellationToken)
    {
        CurrentMapId = mapId;
        if (string.IsNullOrEmpty(mapId))
        {
            Map = new VectorMap();
            MapLoaded = false;
            MapError = "No maps are available.";
            Notify();
            return;
        }

        var result = await vectorMaps.LoadAsync(mapId, cancellationToken);
        if (!result.IsValid)
        {
            Map = new VectorMap();
            MapLoaded = false;
            MapError = string.Join(" ", result.Messages.Select(message => message.Message));
            Notify();
            return;
        }

        Map = result.Value;
        MapLoaded = true;
        MapError = string.Empty;

        await LoadAnnotationsAsync(cancellationToken);

        // Point the shared reveal state at this map; a different map clears prior reveals.
        session.SetCurrentMap(mapId);

        // Seed the player viewport whenever a different map loads (each map has its own world
        // coordinates), to a centred rectangle at the player aspect, and open the doors the
        // annotations marked default-open.
        //
        // "Already seeded" is tracked on the shared session, not here: this workspace is scoped, so a
        // browser refresh (or a second DM window) used to arrive with a blank flag and re-seed —
        // resetting the projector framing and closing every door the DM had opened. The same flag is
        // also restored from the database, so an app restart does not re-seed either.
        if (!string.Equals(mapId, session.SeededMapId, StringComparison.Ordinal))
        {
            session.SetPlayerViewport(MapFraming.DefaultViewport(Map.Bounds, session.PlayerAspect));
            session.SeedOpenDoors(defaultOpenDoorIds);
            session.MarkSeeded(mapId);
        }

        Notify();
    }

    // ---- Navigation -----------------------------------------------------------------------

    /// <summary>
    /// Navigates the briefing panel to a room or area (a manual load, a region click, a connection
    /// or a breadcrumb chip), recording it in the history so the jump is reversible.
    /// </summary>
    /// <param name="node">The area's node id.</param>
    public void NavigateTo(string node)
    {
        if (string.IsNullOrWhiteSpace(node))
        {
            return;
        }

        node = node.Trim();
        history.Push(new NavEntry(node, CrossRefKind.Area, LabelFor(node)));
        Render();
    }

    /// <summary>
    /// Opens a reference for what a link, an occupant or a search result names. A stat block, spell,
    /// entity or book entry opens the reference drawer; an area routes to the panel instead, so a
    /// "place" cross-reference still navigates. One of the campaign's own people, named by roster
    /// id, opens their card in the drawer.
    /// </summary>
    /// <param name="node">The id to open: an area's, a stat block's, an entity's or a book entry's, or an NPC's or player character's roster id.</param>
    public void OpenReference(string node)
    {
        if (string.IsNullOrWhiteSpace(node))
        {
            return;
        }

        node = node.Trim();

        // The roster ids come first: the projection builds no card for a person, so asking it would
        // route the click to the panel as though the person were a room.
        NpcDossier npc = FindNpc(node);
        if (npc.Id.Length > 0)
        {
            history.Push(new NavEntry(node, CrossRefKind.Npc, npc.Name.Length > 0 ? npc.Name : node));
            Render();
            return;
        }

        CharacterDossier character = FindCharacter(node);
        if (character.Id.Length > 0)
        {
            history.Push(new NavEntry(node, CrossRefKind.Character, character.Name.Length > 0 ? character.Name : node));
            Render();
            return;
        }

        var result = content.GetReferenceCard(node);
        ReferenceCard card = result.IsValid ? result.Value : new ReferenceCard();
        if (card.NodeId.Length == 0 || card.Kind == CrossRefKind.Area)
        {
            NavigateTo(node);
            return;
        }

        history.Push(new NavEntry(node, card.Kind, card.Label));
        Render();
    }

    /// <summary>Returns to the previous history stop (closing the drawer when that stop is a room).</summary>
    public void NavigateBack()
    {
        if (history.Back())
        {
            Render();
        }
    }

    /// <summary>Moves forward to the next history stop.</summary>
    public void NavigateForward()
    {
        if (history.Forward())
        {
            Render();
        }
    }

    /// <summary>Jumps the cursor to a history stop (a breadcrumb chip click).</summary>
    /// <param name="index">Index into <see cref="NavHistory.Entries"/>.</param>
    public void JumpTo(int index)
    {
        if (history.JumpTo(index))
        {
            Render();
        }
    }

    /// <summary>
    /// Closes the reference drawer by returning to the nearest room in the history, leaving the
    /// entity stops ahead of it so Forward reopens the card.
    /// </summary>
    public void CloseDrawer()
    {
        int area = history.NearestAreaIndex();
        if (area >= 0)
        {
            history.JumpTo(area);
            Render();
            return;
        }

        CardNode = string.Empty;
        ClearCard();
        Notify();
    }

    /// <summary>
    /// Handles an Inspect-mode click on a map region: navigates to its linked briefing, or — when
    /// the region has no link — highlights it and explains, keeping the current room's briefing (and
    /// the breadcrumb) shown so the panel and the history stay in step.
    /// </summary>
    /// <param name="regionId">The clicked region's id.</param>
    public void SelectRegion(string regionId)
    {
        Region region = regions.Find(r => string.Equals(r.RegionId, regionId, StringComparison.Ordinal));
        if (region is null)
        {
            return;
        }

        if (region.GraphNodeId.Length > 0)
        {
            // A region opens its area. One linked to something else (a person or a creature, from
            // before the Room Editor offered areas only, or a hand edit) opens that one's card
            // instead, over the room on show, rather than a panel saying no area has that id.
            if (IsSomethingOtherThanAnArea(region.GraphNodeId))
            {
                OpenReference(region.GraphNodeId);
                return;
            }

            NavigateTo(region.GraphNodeId);
            return;
        }

        SelectedRegionId = region.RegionId;
        RegionNotice = $"Region '{(region.Label.Length > 0 ? region.Label : region.RegionId)}' has no linked briefing yet.";
        Notify();
    }

    // ---- Reveal bridges -------------------------------------------------------------------

    /// <summary>True when every region belonging to the shown room is revealed to the players.</summary>
    /// <returns><c>true</c> when the whole room is revealed.</returns>
    public bool RoomRevealed()
    {
        if (roomRegionIds.Count == 0)
        {
            return false;
        }

        return AllContained(roomRegionIds, new HashSet<string>(session.RevealedRegionIds(), StringComparer.Ordinal));
    }

    /// <summary>
    /// Reveals or hides the whole shown room for the players by flipping every region that belongs
    /// to it toward the opposite of its current aggregate reveal state.
    /// </summary>
    public void ToggleRoomReveal()
    {
        if (roomRegionIds.Count == 0)
        {
            return;
        }

        var revealed = new HashSet<string>(session.RevealedRegionIds(), StringComparer.Ordinal);
        bool target = !AllContained(roomRegionIds, revealed);
        foreach (string id in roomRegionIds.Where(id => revealed.Contains(id) != target))
        {
            session.ToggleRegion(id);
        }
    }

    // ---- Framing --------------------------------------------------------------------------

    /// <summary>
    /// The world-unit rectangle of the shown room — the union of its linked regions, or the
    /// highlighted region when the room has none. Empty when nothing is selected.
    /// </summary>
    /// <returns>The room's bounding rectangle.</returns>
    public MapBounds SelectedBounds() => MapFraming.Bounds(SelectedRegions());

    /// <summary>
    /// The regions the Info tab's orientation mini map draws: the shown room's regions, or the
    /// highlighted region when the room has no linked geometry.
    /// </summary>
    /// <returns>The regions to highlight (possibly empty).</returns>
    public IReadOnlyList<Region> SelectedRegions()
    {
        if (roomRegionIds.Count > 0)
        {
            var ids = new HashSet<string>(roomRegionIds, StringComparer.Ordinal);
            return regions.Where(region => ids.Contains(region.RegionId)).ToArray();
        }

        if (SelectedRegionId.Length == 0)
        {
            return Array.Empty<Region>();
        }

        return regions
            .Where(region => string.Equals(region.RegionId, SelectedRegionId, StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>
    /// The display name of whatever the player projector is currently pointed at: the region under
    /// the viewport's centre (its own label, or its linked area's title), else an empty string.
    /// </summary>
    /// <returns>The region's display name, or an empty string.</returns>
    public string ViewportRegionName()
    {
        MapBounds viewport = session.PlayerViewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return string.Empty;
        }

        string regionId = MapFraming.RegionAt(
            regions,
            viewport.MinX + (viewport.Width / 2),
            viewport.MinY + (viewport.Height / 2));
        if (regionId.Length == 0)
        {
            return string.Empty;
        }

        Region region = regions.Find(r => string.Equals(r.RegionId, regionId, StringComparison.Ordinal));
        if (region is null)
        {
            return string.Empty;
        }

        if (region.GraphNodeId.Length > 0)
        {
            return LabelFor(region.GraphNodeId);
        }

        return region.Label.Length > 0 ? region.Label : region.RegionId;
    }

    // ---- Lookups --------------------------------------------------------------------------

    /// <summary>
    /// The Search tab's lookup, capped for the panel: the same search the Room Editor's node picker
    /// uses, over the campaign's own content and then the book index.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>The ranked matches, best first.</returns>
    public IReadOnlyList<SearchMatch> SearchNodes(string query) =>
        content.SearchNodes(query, SearchLimit);

    /// <summary>
    /// True when an area node id has an authored dossier. The quest log asks this of each beat's
    /// destination: every front-matter destination sits on a level nobody has authored yet, so the
    /// chip is a muted label today and becomes a live jump on its own once that level lands.
    /// </summary>
    /// <param name="node">The area's node id.</param>
    /// <returns><c>true</c> when a dossier exists for it.</returns>
    public bool IsAreaAuthored(string node) => dossiers.GetArea(node).IsValid;

    /// <summary>
    /// True when a level file writes floor <paramref name="level"/> (<c>level-1.json</c> for floor 1).
    /// The quest log asks this of a beat that happens on a floor rather than in one area.
    /// </summary>
    /// <param name="level">The floor number.</param>
    /// <returns><c>true</c> when that floor is written.</returns>
    public bool IsFloorAuthored(int level) =>
        level > 0 && dossiers.AllLevels().Any(written =>
            dossiers.GetLevelNumber(written.LevelNodeId) is { IsValid: true } number && number.Value == level);

    /// <summary>
    /// Resolves the cross-references in a block of dossier prose, for the panel and drawer links.
    /// The scan is scoped to the floor the shown area sits on, so a bare <c>[[area 19]]</c> means
    /// <em>this</em> floor's room; prose shown while standing on no authored floor resolves only the
    /// area links that name their floor.
    /// </summary>
    /// <param name="text">The prose to scan.</param>
    /// <returns>The resolved reference spans.</returns>
    public IReadOnlyList<CrossRef> ResolveRefs(string text) =>
        crossRefs.Resolve(text, LevelDossier.LevelNodeId);

    /// <summary>
    /// Resolves the cross-references in the open reference card's prose. An entity a level declares
    /// is read on its own floor, so its description's bare "area 6d" means that floor's area whichever
    /// floor the panel is showing; any other card is read on the shown floor, as the panel is.
    /// </summary>
    /// <param name="text">The prose to scan.</param>
    /// <returns>The resolved reference spans.</returns>
    public IReadOnlyList<CrossRef> ResolveCardRefs(string text) =>
        crossRefs.Resolve(text, ReferenceCard.LevelNodeId.Length > 0 ? ReferenceCard.LevelNodeId : LevelDossier.LevelNodeId);

    /// <summary>
    /// Resolves the cross-references in prose that says which floor it belongs to: a relation's note
    /// is read on the floor of the level file that declares it, and a campaign-wide one on none.
    /// </summary>
    /// <param name="text">The prose to scan.</param>
    /// <param name="floor">The level node id the prose belongs to, or an empty string for none.</param>
    /// <returns>The resolved reference spans.</returns>
    public IReadOnlyList<CrossRef> ResolveRefsOn(string text, string floor) =>
        crossRefs.Resolve(text, floor ?? string.Empty);

    /// <summary>
    /// Resolves cross-references in prose that is <em>not about any one floor</em>: the quest log,
    /// the party, the NPCs, the story, the session prep, the decks, the campaign background and the
    /// rules. It is read on no floor, so a bare <c>[[area 3a]]</c> in it is a broken link rather than
    /// the shown floor's area 3a; an area link there names its floor (<c>[[L1 area 3a]]</c>), and then
    /// it is a jump.
    /// </summary>
    /// <remarks>
    /// Every link comes back, area links included: one left out would reach the page as its raw
    /// markup, brackets and all.
    /// </remarks>
    /// <param name="text">The prose to scan.</param>
    /// <returns>The resolved reference spans.</returns>
    public IReadOnlyList<CrossRef> ResolveEntityRefs(string text) =>
        crossRefs.Resolve(text);

    /// <summary>
    /// The display label for a target: the dossier title (authoritative — it carries the level and
    /// area number), then its card's label, else the node id itself.
    /// </summary>
    /// <param name="node">The node id.</param>
    /// <returns>The label to show.</returns>
    public string LabelFor(string node)
    {
        var dossier = dossiers.GetArea(node);
        if (dossier.IsValid && dossier.Value.Title.Length > 0)
        {
            return dossier.Value.Title;
        }

        var card = content.GetReferenceCard(node);
        return card.IsValid && card.Value.Label.Length > 0 ? card.Value.Label : node;
    }

    // ---- Internals ------------------------------------------------------------------------

    // The campaign-scoped documents, read once per circuit and again whenever the content changes: the
    // rules reference, the quest log, the background, the party, the NPCs, the story, the session prep
    // and the decks. None is about the room on show, so none is read per navigation.
    private void LoadCampaignDocuments()
    {
        var reference = dossiers.GetReference();
        ReferenceLibrary = reference.IsValid ? reference.Value : new ReferenceLibrary();

        var quests = dossiers.GetQuests();
        QuestLog = quests.IsValid ? quests.Value : new QuestLog();

        var campaign = dossiers.GetCampaign();
        CampaignDossier = campaign.IsValid ? campaign.Value : new CampaignDossier();

        var party = dossiers.GetParty();
        PartyDossier = party.IsValid ? party.Value : new PartyDossier();

        var npcs = dossiers.GetNpcs();
        NpcRoster = npcs.IsValid ? npcs.Value : new NpcRoster();

        var story = dossiers.GetStory();
        StoryLibrary = story.IsValid ? story.Value : new StoryLibrary();

        var sessions = dossiers.GetSessions();
        SessionLog = sessions.IsValid ? sessions.Value : new SessionLog();

        Decks = dossiers.AllDecks();
    }

    // The loaded map's drawing and notes, read again in place: the same map, so the shared session is
    // not pointed anywhere and nothing is seeded, and the highlighted region stays if it still exists.
    private async Task ReloadCurrentMapAsync(CancellationToken cancellationToken)
    {
        string selected = SelectedRegionId;
        var result = await vectorMaps.LoadAsync(CurrentMapId, cancellationToken);
        if (result.IsValid)
        {
            Map = result.Value;
            MapLoaded = true;
            MapError = string.Empty;
        }
        else
        {
            Map = new VectorMap();
            MapLoaded = false;
            MapError = string.Join(" ", result.Messages.Select(message => message.Message));
        }

        await LoadAnnotationsAsync(cancellationToken);
        if (regions.Exists(region => string.Equals(region.RegionId, selected, StringComparison.Ordinal)))
        {
            SelectedRegionId = selected;
        }
    }

    // The area the DM screen opens on before anything is clicked: the one the campaign names, else the
    // first area of the first level file, else none (an empty campaign opens on an empty panel). A
    // named area is honoured even when nothing is authored for it, so a mistyped id shows up as the
    // panel's own "not found" message rather than as a quietly different room.
    private string StartArea()
    {
        string named = (CampaignDossier.StartAreaNodeId ?? string.Empty).Trim();
        if (named.Length > 0)
        {
            return named;
        }

        return dossiers.AllAreas()
            .Select(area => area.AreaNodeId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty;
    }

    private async Task LoadAnnotationsAsync(CancellationToken cancellationToken)
    {
        regions.Clear();
        markers.Clear();
        objects.Clear();
        secretDoorIds.Clear();
        doubleDoorIds.Clear();
        defaultOpenDoorIds.Clear();
        concealedObjectIds.Clear();
        SelectedRegionId = string.Empty;
        RegionNotice = string.Empty;
        AnnotationsWarning = string.Empty;

        var result = await annotations.LoadAsync(CurrentMapId, cancellationToken);
        if (result.IsValid)
        {
            regions.AddRange(result.Value.Regions);
            markers.AddRange(result.Value.Features);
            objects.AddRange(result.Value.Objects);
            foreach (ObjectAnnotation annotation in result.Value.Objects)
            {
                IndexAnnotation(annotation);
            }
        }
        else if (annotations.Exists(CurrentMapId))
        {
            // The document exists but did not parse: warn the DM (secret doors won't be marked and
            // regions won't reveal until it's fixed). On the player circuit the same failure fails
            // CLOSED — the map is hidden entirely (see PlayerView.LoadAnnotations).
            AnnotationsWarning = "The saved map notes could not be read, so regions and secret-door "
                + "marks are unavailable. Players see a hidden map until this is fixed.";
        }

        // The shown room persists across a map switch; re-resolve its regions against the
        // annotations just loaded (empty when the load failed).
        RecomputeRoomRegions();
    }

    private void IndexAnnotation(ObjectAnnotation annotation)
    {
        // Concealment is authored on stairs and decorations (a door hides as a SECRET door
        // instead, which renders it as wall rather than removing it from the map).
        if (annotation.IsConcealed && annotation.Kind != MapObjectKind.Door)
        {
            concealedObjectIds.Add(annotation.ObjectId);
        }

        if (annotation.Kind != MapObjectKind.Door)
        {
            return;
        }

        if (annotation.IsSecret)
        {
            secretDoorIds.Add(annotation.ObjectId);
        }

        if (annotation.DoubleDoors)
        {
            doubleDoorIds.Add(annotation.ObjectId);
        }

        if (annotation.DefaultOpen)
        {
            defaultOpenDoorIds.Add(annotation.ObjectId);
        }
    }

    // Realise the view for the current history stop: a room drives the panel and closes the drawer;
    // an entity opens the drawer over the room it was reached from.
    private void Render()
    {
        if (history.Cursor < 0)
        {
            return;
        }

        NavEntry current = history.Current;
        if (current.Kind == CrossRefKind.Area)
        {
            CardNode = string.Empty;
            ClearCard();
            SetPanel(current.NodeId);
            Notify();
            return;
        }

        CardNode = current.NodeId;
        ClearCard();
        OpenNpc = FindNpc(CardNode);
        OpenCharacter = FindCharacter(CardNode);
        if (OpenNpc.Id.Length > 0)
        {
            // An NPC who fights shows the block they use beneath their own card, as an entity does.
            string block = content.StatBlockOf(OpenNpc.Id);
            ReferenceCard = new ReferenceCard
            {
                NodeId = OpenNpc.Id,
                Label = OpenNpc.Name,
                Kind = CrossRefKind.Npc,
                Relations = content.RelationsOf(OpenNpc.Id),
                StatBlockId = block,
            };
            OpenStatBlock = block.Length > 0 ? ResultOrEmpty(stats.GetMonster(block)) : new StatBlock();
        }
        else if (OpenCharacter.Id.Length > 0)
        {
            ReferenceCard = new ReferenceCard
            {
                NodeId = OpenCharacter.Id,
                Label = OpenCharacter.Name,
                Kind = CrossRefKind.Character,
                Relations = content.RelationsOf(OpenCharacter.Id),
            };
        }
        else
        {
            var card = content.GetReferenceCard(CardNode);
            ReferenceCard = card.IsValid ? card.Value : new ReferenceCard();

            // An authored individual shows the block it uses beneath its own description; a stat
            // block shows itself. Anything else (a creature with no extracted block, a faction, a
            // book entry) keeps the plain reference card.
            if (ReferenceCard.StatBlockId.Length > 0)
            {
                OpenStatBlock = ResultOrEmpty(stats.GetMonster(ReferenceCard.StatBlockId));
            }
            else
            {
                OpenStatBlock = ReferenceCard.Kind == CrossRefKind.Monster
                    ? ResultOrEmpty(stats.GetMonster(CardNode))
                    : new StatBlock();
            }

            OpenSpellEntry = ReferenceCard.Kind == CrossRefKind.Spell
                ? ResultOrEmptySpell(stats.GetSpell(CardNode))
                : new SpellEntry();
        }

        OpenStatBlockCitation = CitationOf(OpenStatBlock.NodeId);
        OpenSpellCitation = CitationOf(OpenSpellEntry.NodeId);

        // The campaign's own card, extended by what the book says under the same id.
        if (ReferenceCard.Kind != CrossRefKind.Book)
        {
            Result<ReferenceCard> book = content.GetBookCard(CardNode);
            BookCard = book.IsValid ? book.Value : new ReferenceCard();
        }

        string area = history.NearestArea();
        if (area.Length > 0 && !string.Equals(area, PanelNode, StringComparison.Ordinal))
        {
            SetPanel(area);
        }

        Notify();
    }

    // Point the panel at a room: load its briefing and resolve the map region(s) that belong to it.
    private void SetPanel(string node)
    {
        PanelNode = node;
        LoadBriefingFor(node);
        RecomputeRoomRegions();
        SelectedRegionId = roomRegionIds.Count > 0 ? roomRegionIds[0] : string.Empty;
        RegionNotice = string.Empty;
    }

    private void LoadBriefingFor(string node)
    {
        var dossier = dossiers.GetArea(node);
        AreaDossier = dossier.IsValid ? dossier.Value : new AreaDossier();

        var result = content.GetBriefing(node);
        if (result.IsValid)
        {
            Briefing = result.Value;
            ErrorMessage = string.Empty;
        }
        else
        {
            Briefing = new RoomBriefing();
            ErrorMessage = string.Join(" ", result.Messages.Select(message => message.Message));
        }

        var level = dossiers.GetLevelForArea(node);
        LevelDossier = level.IsValid ? level.Value : new LevelDossier();

        Encounter = encounters.For(Briefing);
    }

    private void RecomputeRoomRegions()
    {
        roomRegionIds.Clear();
        if (string.IsNullOrEmpty(PanelNode))
        {
            return;
        }

        roomRegionIds.AddRange(regions
            .Where(region => string.Equals(region.GraphNodeId, PanelNode, StringComparison.Ordinal))
            .Select(region => region.RegionId));
    }

    // Everything the drawer shows, emptied: a card for the next stop is built from scratch rather than
    // inheriting the stat block or the person of the last one.
    private void ClearCard()
    {
        ReferenceCard = new ReferenceCard();
        BookCard = new ReferenceCard();
        OpenStatBlock = new StatBlock();
        OpenSpellEntry = new SpellEntry();
        OpenStatBlockCitation = string.Empty;
        OpenSpellCitation = string.Empty;
        OpenNpc = new NpcDossier();
        OpenCharacter = new CharacterDossier();
    }

    // A stat block's or a spell's citation, as its card gives it: the book's title where the book index
    // knows the source file.
    private string CitationOf(string nodeId)
    {
        if (nodeId.Length == 0)
        {
            return string.Empty;
        }

        var card = content.GetReferenceCard(nodeId);
        return card.IsValid ? card.Value.Citation : string.Empty;
    }

    // True when something is written under an id and it is not an area: a person, or anything with a
    // card of its own. An id nothing is written under yet is not: it is an area waiting for its floor.
    private bool IsSomethingOtherThanAnArea(string id)
    {
        if (dossiers.GetArea(id).IsValid)
        {
            return false;
        }

        if (FindNpc(id).Id.Length > 0 || FindCharacter(id).Id.Length > 0)
        {
            return true;
        }

        var card = content.GetReferenceCard(id);
        return card.IsValid && card.Value.Kind != CrossRefKind.Area;
    }

    // The roster entry with this id, or an empty one. Ids are compared exactly, as authored.
    private NpcDossier FindNpc(string id) =>
        NpcRoster.Npcs.FirstOrDefault(npc => string.Equals(npc.Id, id, StringComparison.Ordinal)) ?? new NpcDossier();

    private CharacterDossier FindCharacter(string id) =>
        PartyDossier.Characters.FirstOrDefault(character => string.Equals(character.Id, id, StringComparison.Ordinal)) ?? new CharacterDossier();

    private void Notify() => Changed?.Invoke();

    private static bool AllContained(IReadOnlyList<string> ids, HashSet<string> set) => ids.All(set.Contains);

    private static StatBlock ResultOrEmpty(Result<StatBlock> result) =>
        result.IsValid ? result.Value : new StatBlock();

    private static SpellEntry ResultOrEmptySpell(Result<SpellEntry> result) =>
        result.IsValid ? result.Value : new SpellEntry();
}
