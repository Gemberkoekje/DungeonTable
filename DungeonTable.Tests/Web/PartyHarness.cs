using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// A DM workspace over a small map built for the party token, with the session and the mover beside it.
/// The squares are 10 world units (5 ft). Along the top, three rooms in a row:
/// <list type="bullet">
/// <item>the Landing (area A), columns 0 to 5;</item>
/// <item>the Nave (area B), columns 6 to 11, behind a solid wall with a door drawn over it
/// (<see cref="NaveDoor"/>), so sight passes only while that door stands open;</item>
/// <item>the Vestry (no area), columns 12 to 17, behind a wall with a gap holding a secret door
/// (<see cref="SecretDoor"/>), so sight passes once the players have found it.</item>
/// </list>
/// Below them, past four rows of rock, a corridor along row 10 (columns 0 to 19, linked to no area) with
/// an alcove off it at column 9, rows 11 to 15, and a region at columns 15 and 16 that links to a
/// creature rather than an area, as an old annotation might.
/// </summary>
internal sealed class PartyHarness
{
    internal const string MapId = "party-map";
    internal const string AreaA = "area-landing";
    internal const string AreaB = "area-nave";
    internal const string NaveDoor = "door-nave";
    internal const string SecretDoor = "door-vestry";
    internal const string Statue = "data_statblocks_monsters_bell_warden";

    private PartyHarness(SessionState session, DmWorkspace workspace, PartyMoves moves)
    {
        Session = session;
        Workspace = workspace;
        Moves = moves;
    }

    public SessionState Session { get; }

    public DmWorkspace Workspace { get; }

    public PartyMoves Moves { get; }

    /// <summary>A started workspace on the party map, opened on the Landing.</summary>
    /// <returns>The harness.</returns>
    public static Task<PartyHarness> Started() => Start(_ => { });

    /// <summary>
    /// A workspace started on a table where the party already stands somewhere: a DM window opened, or
    /// refreshed, in the middle of the evening.
    /// </summary>
    /// <param name="x">Where the token stands, in world units.</param>
    /// <param name="y">Where the token stands, in world units.</param>
    /// <returns>The harness.</returns>
    public static Task<PartyHarness> StartedWithPartyAt(double x, double y) => Start(session =>
    {
        session.SetCurrentMap(MapId);
        session.MarkSeeded(MapId);
        session.MoveParty(x, y, Array.Empty<(int, int)>(), joinLast: false);
    });

    private static async Task<PartyHarness> Start(Action<SessionState> table)
    {
        var content = new FakeContentProjection();
        content.Briefings[AreaA] = new RoomBriefing { RoomId = AreaA, Label = "Landing" };
        content.Briefings[AreaB] = new RoomBriefing { RoomId = AreaB, Label = "Nave" };
        content.Cards[Statue] = new ReferenceCard { NodeId = Statue, Label = "Bell-Warden", Kind = CrossRefKind.Monster };

        var maps = new FakeVectorMapStore();
        maps.Maps[MapId] = Map();

        var annotations = new FakeMapStore();
        annotations.Definitions[MapId] = new MapDefinition
        {
            MapId = MapId,
            Regions = new[]
            {
                Rect("r-landing", AreaA, 0, 0, 60, 60),
                Rect("r-nave", AreaB, 60, 0, 120, 60),
                Rect("r-statue", Statue, 150, 100, 170, 110),
                Rect("r-corridor", string.Empty, 0, 100, 200, 110),
            },
            Objects = new[]
            {
                new ObjectAnnotation { ObjectId = NaveDoor, Kind = MapObjectKind.Door },
                new ObjectAnnotation { ObjectId = SecretDoor, Kind = MapObjectKind.Door, IsSecret = true },
            },
        };

        var dossiers = new FakeDossierStore { Campaign = new CampaignDossier { StartAreaNodeId = AreaA } };
        var crossRefs = new FakeCrossRefResolver();
        var stats = new FakeStatLibrary();
        var session = new SessionState();
        table(session);
        var workspace = new DmWorkspace(
            content, crossRefs, maps, annotations, dossiers, stats, new EncounterSource(crossRefs, content, stats), session);
        await workspace.InitialiseAsync(CancellationToken.None);
        return new PartyHarness(session, workspace, new PartyMoves(workspace, session));
    }

    /// <summary>The middle of a square, in world units.</summary>
    /// <param name="col">The square's column.</param>
    /// <param name="row">The square's row.</param>
    /// <returns>Its middle's X and Y.</returns>
    public static (double X, double Y) Middle(int col, int row) => ((col * 10) + 5, (row * 10) + 5);

    private static VectorMap Map() =>
        new VectorMap
        {
            MapId = MapId,
            Grid = new Grid { CellSizePx = 10 },
            Bounds = new MapBounds(0, 0, 200, 160),
            Layers = new[]
            {
                new MapLayer
                {
                    Kind = LayerKind.Floor,
                    Geometry = new VectorGeometry
                    {
                        Polygons = new[] { Square(0, 0, 180, 60), Square(0, 100, 200, 110), Square(90, 110, 100, 160) },
                    },
                },
                new MapLayer
                {
                    Kind = LayerKind.Walls,
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[]
                        {
                            Line(60, 0, 60, 60),
                            Line(120, 0, 120, 20),
                            Line(120, 40, 120, 60),
                        },
                    },
                },
            },
            Doors = new[]
            {
                new DoorFeature { Id = NaveDoor, Orientation = DoorOrientation.Vertical, Bounds = new MapBounds(58, 20, 62, 40) },
                new DoorFeature { Id = SecretDoor, Orientation = DoorOrientation.Vertical, Bounds = new MapBounds(118, 20, 122, 40) },
            },
        };

    private static Polyline Line(double x0, double y0, double x1, double y1) =>
        new Polyline { Points = new[] { new MapPoint(x0, y0), new MapPoint(x1, y1) } };

    private static MapPolygon Square(double x0, double y0, double x1, double y1) =>
        new MapPolygon
        {
            Rings = new[]
            {
                new PolygonRing
                {
                    Vertices = new[]
                    {
                        new MapPoint(x0, y0), new MapPoint(x1, y0),
                        new MapPoint(x1, y1), new MapPoint(x0, y1), new MapPoint(x0, y0),
                    },
                },
            },
        };

    private static Region Rect(string id, string node, double minX, double minY, double maxX, double maxY) =>
        new Region
        {
            RegionId = id,
            GraphNodeId = node,
            Label = id,
            Polygon = new[]
            {
                new MapPoint(minX, minY),
                new MapPoint(maxX, minY),
                new MapPoint(maxX, maxY),
                new MapPoint(minX, maxY),
            },
        };
}
