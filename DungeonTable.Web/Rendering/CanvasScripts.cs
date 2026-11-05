using System.Globalization;
using System.Linq;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// What a map canvas last told its scripts after a render (pan and zoom, the player-view box, the party
/// token, the editor), so that after the next render it tells them only what changed, and nothing when
/// nothing did. Each question records the answer it gives: asked twice with the same values, it says
/// "changed" once.
/// </summary>
/// <remarks>
/// On a phone's hotspot every call into the browser is a round trip, and the DM's map renders several
/// times a second while the party token moves. The canvas used to make four calls after every render,
/// whatever had changed; now it makes one, carrying the parts this says changed, or none.
/// </remarks>
public sealed class CanvasScripts
{
    private const string PolygonTool = "draw-poly";

    private string viewBox = string.Empty;
    private string playerBox = string.Empty;
    private string party = string.Empty;
    private string editor = string.Empty;

    /// <summary>Whether the pan and zoom must be applied again: the viewBox, and with it the map, changed.</summary>
    /// <param name="current">The canvas's viewBox now.</param>
    /// <returns>True the first time and whenever it differs from the last one.</returns>
    public bool PanZoomChanged(string current) => Changed(ref viewBox, current ?? string.Empty);

    /// <summary>Whether the player-view box must be told where it is.</summary>
    /// <param name="viewport">Where the box is, in world units.</param>
    /// <param name="aspect">The projector's aspect ratio.</param>
    /// <param name="turns">The projector's quarter turns.</param>
    /// <param name="held">Whether the projector shows another map.</param>
    /// <returns>True when any of them differs from what the box was last told.</returns>
    public bool PlayerBoxChanged(MapBounds viewport, double aspect, int turns, bool held) =>
        Changed(ref playerBox, Key(viewport.MinX, viewport.MinY, viewport.Width, viewport.Height, aspect, turns, held ? 1 : 0));

    /// <summary>Whether the party token must be told where it is.</summary>
    /// <param name="token">The party token.</param>
    /// <param name="visionRadius">Its ring's radius, in world units.</param>
    /// <param name="grid">The map's grid, which the token snaps to.</param>
    /// <param name="draggable">Whether it takes the pointer.</param>
    /// <returns>True when any of them differs from what the token was last told.</returns>
    public bool PartyChanged(PartyToken token, double visionRadius, Grid grid, bool draggable) =>
        Changed(ref party, Key(
            token.IsPlaced ? 1 : 0, token.X, token.Y, visionRadius, grid.CellSizePx, grid.OriginX, grid.OriginY, draggable ? 1 : 0));

    /// <summary>
    /// Whether the editor must be told its tool. The polygon tool is told after every render whatever
    /// changed: its rubber band runs from the outline's last corner, which a render moves.
    /// </summary>
    /// <param name="tool">The editor's tool.</param>
    /// <param name="unit">The overlay's unit size.</param>
    /// <param name="grid">The map's grid, whose origin the measuring tool counts from.</param>
    /// <returns>True when the editor must be told.</returns>
    public bool EditorChanged(string tool, double unit, Grid grid)
    {
        bool changed = Changed(ref editor, (tool ?? string.Empty) + "|" + Key(unit, grid.OriginX, grid.OriginY));
        return changed || string.Equals(tool, PolygonTool, StringComparison.Ordinal);
    }

    /// <summary>Records that the player-view box was set up at these values, so they need not be told again.</summary>
    /// <param name="viewport">Where the box is, in world units.</param>
    /// <param name="aspect">The projector's aspect ratio.</param>
    /// <param name="turns">The projector's quarter turns.</param>
    /// <param name="held">Whether the projector shows another map.</param>
    public void PlayerBoxSetUp(MapBounds viewport, double aspect, int turns, bool held) =>
        PlayerBoxChanged(viewport, aspect, turns, held);

    /// <summary>
    /// The script moved the box itself (the DM dragged it): tell it after the next render whatever the
    /// server says, since the server may have put it back where it was.
    /// </summary>
    public void ForgetPlayerBox() => playerBox = string.Empty;

    /// <summary>
    /// The script moved the token itself (the DM dragged or clicked it): tell it after the next render
    /// whatever the server says, since the server may have it where it was.
    /// </summary>
    public void ForgetParty() => party = string.Empty;

    private static bool Changed(ref string told, string now)
    {
        if (string.Equals(told, now, StringComparison.Ordinal))
        {
            return false;
        }

        told = now;
        return true;
    }

    private static string Key(params double[] values) =>
        string.Join('|', values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
}
