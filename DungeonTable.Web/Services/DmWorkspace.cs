using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Content;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Web.Services;

/// <summary>
/// Everything the DM screen's map and dock share: the loaded vector map and its editor annotations,
/// the room briefing currently on show, and the reference history that threads them together.
/// Registered <b>Scoped</b>, so there is one per Blazor circuit (one DM browser) — unlike
/// <see cref="SessionState"/>, which is the singleton the player projector also observes. Splitting
/// this out of the DM page keeps the map and the dock's tabs from prop-drilling a dozen parameters
/// each, and lets a tab mutate shared state without knowing who else is watching:
/// <see cref="Changed"/> tells the shell to re-render all of them.
/// </summary>
public sealed class DmWorkspace
{
    // Find anything lists at most this many things by name, then this many passages by their words.
    private const int NameLimit = 6;
    private const int TextLimit = 24;

    /// <summary>How many grid cells around a fight's room the battle layout's map shows.</summary>
    public const int SurroundingCells = 3;

    private readonly IContentProjection content;
    private readonly ICrossRefResolver crossRefs;
    private readonly IVectorMapStore vectorMaps;
    private readonly IMapStore annotations;
    private readonly IDossierStore dossiers;
    private readonly IStatLibrary stats;
    private readonly EncounterSource encounters;
    private readonly SessionState session;
    private readonly CampaignState campaign;

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

    // What is in each room, by its node id, worked out the first time a room is asked about and kept
    // until the content or the map's notes change (see GlanceOf).
    private readonly Dictionary<string, RoomGlance> glances = new Dictionary<string, RoomGlance>(StringComparer.Ordinal);

    // The area the party token stood in when this workspace last followed it, so it can tell when the
    // party has walked into another one.
    private string followedPartyArea = string.Empty;

    // The creatures the shown area lists, before the ones the DM moved here are added (see Encounter).
    private IReadOnlyList<EncounterMonster> listedEncounter = Array.Empty<EncounterMonster>();

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
    /// <param name="campaign">The table's record: what was dealt with where, and the creatures the DM moved.</param>
    public DmWorkspace(
        IContentProjection content,
        ICrossRefResolver crossRefs,
        IVectorMapStore vectorMaps,
        IMapStore annotations,
        IDossierStore dossiers,
        IStatLibrary stats,
        EncounterSource encounters,
        SessionState session,
        CampaignState campaign)
    {
        this.content = content;
        this.crossRefs = crossRefs;
        this.vectorMaps = vectorMaps;
        this.annotations = annotations;
        this.dossiers = dossiers;
        this.stats = stats;
        this.encounters = encounters;
        this.session = session;
        this.campaign = campaign;
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
    /// The creatures in the shown area: the ones it lists, with their authored counts, worked out once
    /// per shown area, and after them the ones the DM moved here, which are table state and so are added
    /// each time this is read (<see cref="EncounterSource.WithMoves"/>).
    /// </summary>
    public IReadOnlyList<EncounterMonster> Encounter => encounters.WithMoves(PanelNode, listedEncounter, campaign);

    /// <summary>
    /// The passage open as a sheet over the dock (a rules section, a floor's fact, a room's detail that
    /// Find anything found); its <see cref="Passage.Key"/> is blank when none is. It stands outside the
    /// reference history: it is a lookup, put away with Esc, and following a link in it or going to a room
    /// puts it away too.
    /// </summary>
    public Passage OpenPassage { get; private set; } = new Passage();

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
        // not point the shared session at the first map, which moves the projector off the one in play.
        string tableMap = session.CurrentMapId;
        string first = MapIds.Count > 0 ? MapIds[0] : string.Empty;
        await LoadMapAsync(MapIds.Contains(tableMap) ? tableMap : first, cancellationToken);

        // A table the party is already walking opens on the party's room; a new one on the start area.
        followedPartyArea = PartyArea;
        NavigateTo(followedPartyArea.Length > 0 ? followedPartyArea : StartArea());
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

        glances.Clear();
        if (changes.HasFlag(ContentChanges.Documents))
        {
            string passage = OpenPassage.Key;
            LoadCampaignDocuments();
            if (history.Cursor >= 0)
            {
                Render();
            }

            // The passage on the sheet, read from the new content; gone when its block is.
            OpenPassage = passage.Length > 0 ? PassageFor(passage) : new Passage();
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

        // Point the shared reveal state at this map. A different map sets the previous one's reveals
        // aside, and brings back this one's if the table has been on it before.
        session.SetCurrentMap(mapId);

        // Seed the player viewport the first time a map is shown (each map has its own world
        // coordinates), to a centred rectangle at the player aspect, and open the doors the
        // annotations marked default-open. A map the table comes back to keeps what it had.
        //
        // "Already seeded" is tracked on the shared session, not here: this workspace is scoped, so a
        // browser refresh (or a second DM window) used to arrive with a blank flag and re-seed —
        // resetting the projector framing and closing every door the DM had opened. The same flag is
        // also restored from the database, so an app restart does not re-seed either.
        if (!string.Equals(mapId, session.SeededMapId, StringComparison.Ordinal))
        {
            session.SetPlayerViewport(MapFraming.DefaultViewport(Map.Bounds, session.FramingAspect));
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
    /// Handles a Look click on a map region: navigates to its linked briefing, or — when
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

    // ---- Following the party --------------------------------------------------------------

    /// <summary>
    /// True when the DM pinned the dock: it stays on what they show it when the party walks into another
    /// room. One DM window's choice, not the table's.
    /// </summary>
    public bool Pinned { get; private set; }

    /// <summary>
    /// The area the party token stands in on the loaded map: the area of the region under it. An empty
    /// string when the token is off the map, or stands where no region opens an area (a corridor nobody
    /// linked, say).
    /// </summary>
    public string PartyArea
    {
        get
        {
            PartyToken token = session.Party;
            if (!MapLoaded || !token.IsPlaced)
            {
                return string.Empty;
            }

            string regionId = MapFraming.RegionAt(regions, token.X, token.Y);
            Region region = regions.Find(r => string.Equals(r.RegionId, regionId, StringComparison.Ordinal));
            if (region is null || region.GraphNodeId.Length == 0 || IsSomethingOtherThanAnArea(region.GraphNodeId))
            {
                return string.Empty;
            }

            return region.GraphNodeId;
        }
    }

    /// <summary>True when the Room tab shows a room other than the one the party token is in.</summary>
    public bool LookingAway
    {
        get
        {
            string party = PartyArea;
            return party.Length > 0 && !string.Equals(party, PanelNode, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Follows the party: when the token has walked into another area since the last call, the Room tab
    /// shows that area, unless the dock is pinned. Looking at another room lasts until the party moves
    /// on; pinning is how a room stays. Call it whenever the shared state changes, on the DM circuit's
    /// own dispatcher: the token is moved by this DM window, another one, or an undo.
    /// </summary>
    public void FollowParty()
    {
        string area = PartyArea;
        if (string.Equals(area, followedPartyArea, StringComparison.Ordinal))
        {
            return;
        }

        followedPartyArea = area;
        if (area.Length == 0 || Pinned || string.Equals(area, PanelNode, StringComparison.Ordinal))
        {
            return;
        }

        NavigateTo(area);
    }

    /// <summary>Pins the dock to what it shows, or lets it follow the party again.</summary>
    public void TogglePin()
    {
        Pinned = !Pinned;
        Notify();
    }

    /// <summary>Shows the party's room in the dock again, and lets the dock follow the party.</summary>
    public void BackToParty()
    {
        Pinned = false;
        string area = PartyArea;
        if (area.Length > 0 && !string.Equals(area, PanelNode, StringComparison.Ordinal))
        {
            NavigateTo(area);
            return;
        }

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
    /// Moves the player viewport onto the shown room, padded by a cell and in the projector's shape,
    /// or onto the whole map when the shown room has no region on this map. The bar's Frame button and
    /// the Room tab's frame action both do this.
    /// </summary>
    public void FrameShownArea()
    {
        if (!MapLoaded)
        {
            return;
        }

        MapBounds target = SelectedBounds();
        if (target.Width <= 0 && target.Height <= 0)
        {
            session.SetPlayerViewport(MapFraming.DefaultViewport(Map.Bounds, session.FramingAspect));
            return;
        }

        MapBounds padded = MapFraming.Pad(target, UnitSize);
        session.SetPlayerViewport(MapFraming.Frame(padded, session.FramingAspect, Map.Bounds));
    }

    /// <summary>
    /// The size of the projector's view now, in grid squares across and down the projector's own
    /// screen (which is the other way round on the map while the picture lies on its side).
    /// </summary>
    /// <returns>The size; <see cref="ViewSize.None"/> with no map or no view.</returns>
    public ViewSize PlayerViewSize()
    {
        MapBounds view = session.PlayerViewport;
        if (!MapLoaded || view.Width <= 0 || view.Height <= 0)
        {
            return ViewSize.None;
        }

        bool onItsSide = session.PlayerTurns % 2 == 1;
        double across = (onItsSide ? view.Height : view.Width) / UnitSize;
        double down = (onItsSide ? view.Width : view.Height) / UnitSize;
        return new ViewSize(across, down);
    }

    /// <summary>Remembers the size of the projector's view now, to frame another map at the same scale.</summary>
    public void RememberPlayerViewSize() => session.RememberView(PlayerViewSize());

    /// <summary>
    /// Sizes the projector's view to the remembered size, about its middle, so a square on the table is
    /// the size it was on the map the size was remembered on.
    /// </summary>
    public void UseRememberedViewSize()
    {
        if (!MapLoaded || !session.RememberedView.IsSet)
        {
            return;
        }

        session.SetPlayerViewport(MapFraming.AtSize(
            session.PlayerViewport, session.RememberedView, UnitSize, session.PlayerAspect, session.PlayerTurns, Map.Bounds));
    }

    /// <summary>
    /// The middle of the shown room, in world units, for centring the DM's map on it; the middle of the
    /// whole map when the room has no region on this map.
    /// </summary>
    /// <returns>The point to centre on.</returns>
    public MapPoint ShownAreaCentre()
    {
        MapBounds bounds = SelectedBounds();
        if (bounds.Width <= 0 && bounds.Height <= 0)
        {
            bounds = Map.Bounds;
        }

        return new MapPoint(bounds.MinX + (bounds.Width / 2), bounds.MinY + (bounds.Height / 2));
    }

    /// <summary>
    /// A room and the cells around it, in world units: what the map frames when the dock goes to the
    /// fight, so the doors, pillars and markers next to the fight are in view too. Empty when the room
    /// has no region on the map on show, so there is nothing to frame.
    /// </summary>
    /// <param name="areaNode">The room's node id.</param>
    /// <returns>The room's regions' bounds, padded by <see cref="SurroundingCells"/> grid cells, or an empty rectangle.</returns>
    public MapBounds AreaSurroundings(string areaNode)
    {
        if (!MapLoaded || string.IsNullOrEmpty(areaNode))
        {
            return default;
        }

        MapBounds bounds = MapFraming.Bounds(regions
            .Where(region => string.Equals(region.GraphNodeId, areaNode, StringComparison.Ordinal))
            .ToArray());
        return bounds.Width <= 0 && bounds.Height <= 0
            ? default
            : MapFraming.Pad(bounds, SurroundingCells * UnitSize);
    }

    /// <summary>
    /// The world-unit rectangle of the shown room — the union of its linked regions, or the
    /// highlighted region when the room has none. Empty when nothing is selected.
    /// </summary>
    /// <returns>The room's bounding rectangle.</returns>
    public MapBounds SelectedBounds() => MapFraming.Bounds(SelectedRegions());

    /// <summary>
    /// The regions of the shown room on this map, or the highlighted region when the room has no
    /// linked geometry: what framing and centring aim at.
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
    /// Find anything: the things whose names match first (the search the Room Editor's node picker uses,
    /// over the campaign's own content and then the book index), then the passages whose words match,
    /// each with the words around the match.
    /// </summary>
    /// <param name="query">What the DM typed.</param>
    /// <returns>The rows, best first; none for a blank query.</returns>
    public IReadOnlyList<FindResult> Find(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<FindResult>();
        }

        IEnumerable<FindResult> names = content.SearchNodes(query, NameLimit).Select(match => new FindResult
        {
            Kind = FindResultKind.Name,
            Label = match.Label.Length > 0 ? match.Label : match.Id,
            Category = PassageWording.NameCategory(match.Category),
            Where = match.Citation,
            Target = match.Id,
        });

        IEnumerable<FindResult> text = content.FindText(query, TextLimit).Select(hit => new FindResult
        {
            Kind = FindResultKind.Text,
            Label = hit.Passage.Title,
            Category = PassageWording.Category(hit.Passage.Kind),
            Where = hit.Passage.Trail,
            Snippet = hit.Snippet,
            Secret = hit.Passage.Secret,
            Target = hit.Passage.Key,
        });

        return names.Concat(text).ToList();
    }

    /// <summary>
    /// Opens a passage as a sheet over the dock, by its key: a Find result's, or a pinned one's. A key that
    /// names nothing (any more) puts the sheet away.
    /// </summary>
    /// <param name="key">The passage's key.</param>
    public void ShowPassage(string key)
    {
        OpenPassage = PassageFor(key);
        Notify();
    }

    /// <summary>Puts away the passage open over the dock, if one is.</summary>
    public void ClosePassage()
    {
        if (OpenPassage.Key.Length == 0)
        {
            return;
        }

        OpenPassage = new Passage();
        Notify();
    }

    /// <summary>A passage as the content holds it now, by its key.</summary>
    /// <param name="key">The passage's key.</param>
    /// <returns>The passage; an empty one (its key blank) when no passage has that key.</returns>
    public Passage PassageFor(string key)
    {
        Result<Passage> found = content.GetPassage(key);
        return found.IsValid ? found.Value : new Passage();
    }

    /// <summary>
    /// Everything a card shows for an id, worked out the way the card sheet works it out: a roster
    /// person's own card (with the block an NPC fights with), a player character's, an entity's with the
    /// block it uses, a stat block or a spell with itself, and what the book adds under the same id.
    /// </summary>
    /// <param name="id">The id a link opens the card by.</param>
    /// <returns>The card's content; an empty card (its node id blank) when nothing has the id.</returns>
    public CardContent CardFor(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new CardContent();
        }

        string node = id.Trim();
        NpcDossier npc = FindNpc(node);
        CharacterDossier character = FindCharacter(node);
        ReferenceCard card;
        StatBlock block = new StatBlock();
        SpellEntry spell = new SpellEntry();
        if (npc.Id.Length > 0)
        {
            // An NPC who fights shows the block they use beneath their own card, as an entity does.
            string uses = content.StatBlockOf(npc.Id);
            card = new ReferenceCard
            {
                NodeId = npc.Id,
                Label = npc.Name,
                Kind = CrossRefKind.Npc,
                Relations = content.RelationsOf(npc.Id),
                StatBlockId = uses,
            };
            block = uses.Length > 0 ? ResultOrEmpty(stats.GetMonster(uses)) : new StatBlock();
        }
        else if (character.Id.Length > 0)
        {
            card = new ReferenceCard
            {
                NodeId = character.Id,
                Label = character.Name,
                Kind = CrossRefKind.Character,
                Relations = content.RelationsOf(character.Id),
            };
        }
        else
        {
            Result<ReferenceCard> found = content.GetReferenceCard(node);
            card = found.IsValid ? found.Value : new ReferenceCard();

            // An authored individual shows the block it uses beneath its own description; a stat block
            // shows itself. Anything else (a creature with no extracted block, a faction, a book entry)
            // keeps the plain reference card.
            if (card.StatBlockId.Length > 0)
            {
                block = ResultOrEmpty(stats.GetMonster(card.StatBlockId));
            }
            else if (card.Kind == CrossRefKind.Monster)
            {
                block = ResultOrEmpty(stats.GetMonster(node));
            }

            if (card.Kind == CrossRefKind.Spell)
            {
                spell = ResultOrEmptySpell(stats.GetSpell(node));
            }
        }

        // The campaign's own card, extended by what the book says under the same id.
        ReferenceCard book = new ReferenceCard();
        if (card.Kind != CrossRefKind.Book)
        {
            Result<ReferenceCard> found = content.GetBookCard(node);
            book = found.IsValid ? found.Value : new ReferenceCard();
        }

        return new CardContent
        {
            Card = card,
            BookCard = book,
            StatBlock = block,
            SpellEntry = spell,
            StatBlockCitation = CitationOf(block.NodeId),
            SpellCitation = CitationOf(spell.NodeId),
            Npc = npc,
            Character = character,
        };
    }

    /// <summary>
    /// Resolves the cross-references in a card's prose on the card's own floor: an entity a level declares
    /// on that level, anything else on the shown floor, as <see cref="ResolveCardRefs"/> does for the card
    /// on the sheet.
    /// </summary>
    /// <param name="card">The card the prose belongs to.</param>
    /// <param name="text">The prose to scan.</param>
    /// <returns>The resolved reference spans.</returns>
    public IReadOnlyList<CrossRef> ResolveRefsOnCard(ReferenceCard card, string text) =>
        crossRefs.Resolve(text, card is not null && card.LevelNodeId.Length > 0 ? card.LevelNodeId : LevelDossier.LevelNodeId);

    /// <summary>
    /// What is in a room at a glance: its creatures, loot, checks, traps, secrets and ways out, for the
    /// badges on the map, the peek card and the Room tab's Nearby list. Worked out the first time it is
    /// asked for and kept until the content changes or another map is loaded, since the traps and
    /// secret doors are read from the map on show; a room on another floor gets the content's part only.
    /// </summary>
    /// <param name="areaNode">The area's node id.</param>
    /// <returns>The room's glance; <see cref="RoomGlance.None"/> when no dossier is authored for it.</returns>
    public RoomGlance GlanceOf(string areaNode)
    {
        if (string.IsNullOrEmpty(areaNode))
        {
            return RoomGlance.None;
        }

        if (!glances.TryGetValue(areaNode, out RoomGlance glance))
        {
            glance = BuildGlance(areaNode);
            glances[areaNode] = glance;
        }

        // The creatures the DM moved here are the table's, not the content's, so they are never kept.
        IReadOnlyList<EncounterMonster> creatures = encounters.WithMoves(areaNode, glance.Creatures, campaign);
        return ReferenceEquals(creatures, glance.Creatures) || glance.IsEmpty ? glance : glance.WithCreatures(creatures);
    }

    // ---- Moving creatures -----------------------------------------------------------------

    /// <summary>
    /// Moves some of a creature from one area to another, as the DM decides it: the guard who stayed
    /// behind when the boss fled, the survivors who fall back. One somebody moves whole; a kind of
    /// creature or a band moves as many as asked, no more than are still there.
    /// </summary>
    /// <param name="fromAreaNodeId">The area they are in now.</param>
    /// <param name="row">The creature, as the area's encounter lists it.</param>
    /// <param name="toAreaNodeId">The area they go to.</param>
    /// <param name="count">How many go; ignored for one somebody.</param>
    public void MoveCreature(string fromAreaNodeId, EncounterMonster row, string toAreaNodeId, int count)
    {
        if (row is null
            || string.IsNullOrWhiteSpace(fromAreaNodeId)
            || string.IsNullOrWhiteSpace(toAreaNodeId)
            || string.Equals(fromAreaNodeId, toAreaNodeId, StringComparison.Ordinal))
        {
            return;
        }

        CreatureStanding standing = CreatureStanding.Of(fromAreaNodeId, row, campaign);
        if (standing.Gone)
        {
            return;
        }

        // A count still to be rolled is as many as the DM says; any other is no more than are left.
        int most = standing.Rolled ? EncounterCount.MaxCount : standing.Left;
        string listedIn = row.MovedHere
            ? campaign.MovesInto(fromAreaNodeId).LastOrDefault(move => string.Equals(move.CreatureId, row.NodeId, StringComparison.Ordinal))?.ListedInAreaNodeId ?? fromAreaNodeId
            : fromAreaNodeId;

        campaign.Move(
            new CreatureMove
            {
                CreatureId = row.NodeId,
                FromAreaNodeId = fromAreaNodeId,
                ToAreaNodeId = toAreaNodeId,
                Count = row.Individual ? 1 : Math.Clamp(count, 1, most),
                ListedInAreaNodeId = listedIn,
            },
            ListedCount(toAreaNodeId, row.NodeId),
            row.Individual);
    }

    /// <summary>
    /// The rooms a creature in the shown area can be moved to, best first. With nothing typed, the rooms
    /// this one leads to, then the rest of its floor; with something typed, the rooms whose title has it
    /// (or whose number it is), this floor's first. Never the shown area itself.
    /// </summary>
    /// <param name="filter">What the DM typed; blank for none.</param>
    /// <param name="limit">How many rooms at most.</param>
    /// <returns>The rooms, as their dossiers.</returns>
    public IReadOnlyList<AreaDossier> MoveTargets(string filter, int limit)
    {
        string typed = (filter ?? string.Empty).Trim();
        string floor = LevelDossier.LevelNodeId;
        string[] exits = Briefing.Connections
            .Where(exit => exit is not null && exit.TargetRoomId.Length > 0)
            .Select(exit => exit.TargetRoomId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return dossiers.AllAreas()
            .Where(area => area.AreaNodeId.Length > 0 && !string.Equals(area.AreaNodeId, PanelNode, StringComparison.Ordinal))
            .Where(area => typed.Length == 0 || Matches(area, typed))
            .Select(area => (Area: area, Exit: Array.IndexOf(exits, area.AreaNodeId), SameFloor: IsOnFloor(area.AreaNodeId, floor)))
            .Where(choice => typed.Length > 0 || choice.Exit >= 0 || choice.SameFloor)
            .OrderBy(choice => typed.Length == 0 && choice.Exit >= 0 ? choice.Exit : int.MaxValue)
            .ThenBy(choice => choice.SameFloor ? 0 : 1)
            .ThenBy(choice => choice.Area.AreaNumber)
            .ThenBy(choice => choice.Area.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(limit, 0))
            .Select(choice => choice.Area)
            .ToArray();
    }

    private static bool Matches(AreaDossier area, string typed) =>
        area.Title.Contains(typed, StringComparison.OrdinalIgnoreCase)
        || (int.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number == area.AreaNumber);

    private bool IsOnFloor(string areaNodeId, string floor)
    {
        if (floor.Length == 0)
        {
            return false;
        }

        Result<LevelDossier> level = dossiers.GetLevelForArea(areaNodeId);
        return level.IsValid && string.Equals(level.Value.LevelNodeId, floor, StringComparison.Ordinal);
    }

    // How many of a creature an area lists itself: what a whole-listing tick there covered.
    private int ListedCount(string areaNodeId, string creatureNodeId)
    {
        Result<RoomBriefing> briefing = content.GetBriefing(areaNodeId);
        EncounterMonster listed = briefing.IsValid
            ? encounters.For(briefing.Value).FirstOrDefault(row => string.Equals(row.NodeId, creatureNodeId, StringComparison.Ordinal))
            : null;
        return listed is null ? 0 : Math.Max(listed.SuggestedCount, 1);
    }

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

    /// <summary>
    /// What a card is called, for a pin's chip: one of the campaign's people by their name, anything else
    /// by its card's label. Cheaper than <see cref="CardFor"/>, which also builds the stat block.
    /// </summary>
    /// <param name="id">The id a link opens the card by.</param>
    /// <returns>The card's name; an empty string when nothing has the id.</returns>
    public string CardLabel(string id)
    {
        NpcDossier npc = FindNpc(id ?? string.Empty);
        if (npc.Id.Length > 0)
        {
            return npc.Name.Length > 0 ? npc.Name : npc.Id;
        }

        CharacterDossier character = FindCharacter(id ?? string.Empty);
        if (character.Id.Length > 0)
        {
            return character.Name.Length > 0 ? character.Name : character.Id;
        }

        Result<ReferenceCard> card = content.GetReferenceCard(id);
        return card.IsValid ? card.Value.Label : string.Empty;
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

        var background = dossiers.GetCampaign();
        CampaignDossier = background.IsValid ? background.Value : new CampaignDossier();

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
        glances.Clear();
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

        // Wherever the history goes, a passage open over the dock is put away: the DM followed a link in it,
        // went to a room, or the dock followed the party.
        OpenPassage = new Passage();

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
        CardContent shown = CardFor(CardNode);
        ReferenceCard = shown.Card;
        BookCard = shown.BookCard;
        OpenStatBlock = shown.StatBlock;
        OpenSpellEntry = shown.SpellEntry;
        OpenStatBlockCitation = shown.StatBlockCitation;
        OpenSpellCitation = shown.SpellCitation;
        OpenNpc = shown.Npc;
        OpenCharacter = shown.Character;

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

        listedEncounter = encounters.For(Briefing);
    }

    private RoomGlance BuildGlance(string areaNode)
    {
        Result<AreaDossier> dossier = dossiers.GetArea(areaNode);
        if (!dossier.IsValid)
        {
            return RoomGlance.None;
        }

        Result<RoomBriefing> found = content.GetBriefing(areaNode);
        RoomBriefing briefing = found.IsValid ? found.Value : new RoomBriefing();
        RoomMarks marks = MapLoaded
            ? RoomMarks.Within(
                areaNode,
                regions.Where(region => string.Equals(region.GraphNodeId, areaNode, StringComparison.Ordinal)).ToArray(),
                markers,
                Map.Doors.Where(door => secretDoorIds.Contains(door.Id)).Select(door => door.Bounds).ToArray(),
                UnitSize)
            : RoomMarks.None;

        return RoomGlance.From(dossier.Value, briefing, encounters.For(briefing), marks);
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
