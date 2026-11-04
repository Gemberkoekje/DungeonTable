using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Web.Services;

/// <summary>
/// Moves the party token and reveals what the party sees: the DM's map hands it a drag a square at a
/// time, a token put down with the Party tool, an opened door or a new vision range, and this works
/// out the tiles from the map in the DM's workspace and puts them on the shared session. Revealing is
/// moving the party, not a tool the DM has to remember to pick.
/// </summary>
/// <remarks>
/// <para>
/// Registered <b>Scoped</b>, one per DM circuit, beside the <see cref="DmWorkspace"/> it reads the map
/// from. The token, what it revealed and the undo of it are the table's, on <see cref="SessionState"/>.
/// </para>
/// <para>
/// The rule is the old lifter's (<see cref="VisionReveal"/>): sample points, corners, open doors, and
/// secret doors the players have not found reading as wall. It is only run from more places, which the
/// lifter's speed allows: a 120 ft lift on a large level takes 10 to 25 ms in a Release build.
/// </para>
/// </remarks>
public sealed class PartyMoves
{
    private readonly DmWorkspace workspace;
    private readonly SessionState session;

    // The squares the drag under way has already looked from, so walking back over them does not lift
    // again. A new move, an opened door or a new range starts it afresh, since each can change what a
    // square shows.
    private readonly HashSet<(int Col, int Row)> walked = new HashSet<(int Col, int Row)>();

    /// <summary>Creates the mover over one DM circuit's workspace and the shared session.</summary>
    /// <param name="workspace">The DM's workspace: the loaded map and its doors.</param>
    /// <param name="session">The shared session: the token, the reveals, the projector's frame.</param>
    public PartyMoves(DmWorkspace workspace, SessionState session)
    {
        this.workspace = workspace;
        this.session = session;
    }

    /// <summary>
    /// How far the party sees, in world units on the loaded map: the radius of the faint ring round the
    /// token. Zero on a map with no calibrated grid, where a range in feet means nothing.
    /// </summary>
    public double VisionRadius =>
        workspace.MapLoaded && workspace.Map.Grid.CellSizePx > 0
            ? session.Party.VisionFeet / MapMeasure.FeetPerCell * workspace.Map.Grid.CellSizePx
            : 0;

    /// <summary>
    /// Moves the token to a point, snapped to the middle of its square and kept on the map, and reveals
    /// what the party saw on the way (see <see cref="PartyMoveKind"/>). When the projector follows the
    /// party and the token has left the middle of its view, the projector is framed on it again.
    /// </summary>
    /// <param name="worldX">Where the token was put, in world units.</param>
    /// <param name="worldY">Where the token was put, in world units.</param>
    /// <param name="kind">How it got there.</param>
    public void Move(double worldX, double worldY, PartyMoveKind kind)
    {
        if (!workspace.MapLoaded || kind == PartyMoveKind.None || !double.IsFinite(worldX) || !double.IsFinite(worldY))
        {
            return;
        }

        VectorMap map = workspace.Map;
        MapPoint to = Snap(map, worldX, worldY);
        PartyToken token = session.Party;
        if (kind != PartyMoveKind.WalkOn)
        {
            walked.Clear();
        }

        MapPoint from = kind != PartyMoveKind.Place && token.IsPlaced ? new MapPoint(token.X, token.Y) : to;
        (int Col, int Row)[] standing = PartyVision.Path(map.Grid, from, to).Where(walked.Add).ToArray();

        IReadOnlyList<(int Col, int Row)> seen = PartyVision.SeenFrom(
            map, standing, token.VisionFeet, BlockingDoorIds(), session.OpenDoorIds());
        session.MoveParty(to.X, to.Y, seen, joinLast: kind == PartyMoveKind.WalkOn);
        FollowWithProjector();
    }

    /// <summary>
    /// Sets how far the party sees, and reveals what the new range shows from where the token stands.
    /// </summary>
    /// <param name="feet">The vision range in feet; brought into bounds.</param>
    public void SetVision(int feet)
    {
        session.SetPartyVision(feet);
        LookAgain(DoorState.None);
    }

    /// <summary>
    /// Reveals what the party sees now that a door changed: an opened door shows the room beyond it
    /// without a second action. Ctrl+Z then takes back what it showed and puts the door back.
    /// </summary>
    /// <param name="before">The door as it stood before the click changed it.</param>
    public void AfterDoor(DoorState before) => LookAgain(before);

    /// <summary>Takes the token off the map. What it revealed stays revealed.</summary>
    public void Remove() => session.RemoveParty();

    /// <summary>
    /// Turns the projector's following of the party on or off. Turning it on frames the projector on
    /// the party straight away if the token is not in the middle of its view.
    /// </summary>
    /// <param name="follow">True to follow.</param>
    public void SetProjectorFollows(bool follow)
    {
        session.SetProjectorFollowsParty(follow);
        FollowWithProjector();
    }

    // The token's point, kept on the map and snapped to the middle of its square, which is where the
    // lifter has always stood the viewer. A map with no grid keeps the point as it is. The far edges are
    // kept just inside, since a point on one belongs to the square beyond it, off the map.
    private static MapPoint Snap(VectorMap map, double worldX, double worldY)
    {
        MapBounds bounds = map.Bounds;
        double x = worldX, y = worldY;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            x = Math.Clamp(x, bounds.MinX, Math.BitDecrement(bounds.MaxX));
            y = Math.Clamp(y, bounds.MinY, Math.BitDecrement(bounds.MaxY));
        }

        return GridCells.CellCentre(map.Grid, x, y);
    }

    private void LookAgain(DoorState door)
    {
        walked.Clear();
        PartyToken token = session.Party;
        if (!workspace.MapLoaded || !token.IsPlaced)
        {
            return;
        }

        VectorMap map = workspace.Map;
        IReadOnlyList<(int Col, int Row)> seen = PartyVision.SeenFrom(
            map, new[] { GridCells.CellAt(map.Grid, token.X, token.Y) }, token.VisionFeet, BlockingDoorIds(), session.OpenDoorIds());
        session.RevealFromParty(seen, door);
    }

    // A secret door the players have found is a door like any other; one they have not reads as wall.
    private HashSet<string> BlockingDoorIds()
    {
        var blocking = new HashSet<string>(workspace.SecretDoorIds, StringComparer.Ordinal);
        blocking.ExceptWith(session.RevealedFeatureIds());
        return blocking;
    }

    private void FollowWithProjector()
    {
        PartyToken token = session.Party;
        if (!session.ProjectorFollowsParty || !token.IsPlaced || !workspace.MapLoaded)
        {
            return;
        }

        MapBounds viewport = session.PlayerViewport;
        MapBounds framed = MapFraming.KeepInMiddle(viewport, token.X, token.Y, workspace.Map.Bounds);
        if (framed != viewport)
        {
            session.SetPlayerViewport(framed);
        }
    }
}
