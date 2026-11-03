namespace DungeonTable.Web.Services;

/// <summary>
/// Which map tool is up, and when the map goes back to <see cref="MapTool.Look"/> by itself.
/// </summary>
/// <remarks>
/// The trap this exists to remove is getting stuck in the wrong mode: after a reveal the DM had to
/// switch modes to read a room, and after reading, switch back. So the tools that do one thing to
/// one object (reveal a room, a secret door, a concealed object, a point on the projector) are
/// <b>one-click</b>: once a click has done something, the map is back in Look. The tools that are
/// used in a run (the fog brush, the lifter, measuring) stay until Esc. Picking a one-click tool
/// with Shift held keeps it up, for the evening the DM reveals a whole wing room by room.
/// </remarks>
public sealed class ToolSelection
{
    /// <summary>The tool up now. Look unless one was picked.</summary>
    public MapTool Active { get; private set; } = MapTool.Look;

    /// <summary>True when a one-click tool was picked with Shift held, so it stays up after use.</summary>
    public bool Kept { get; private set; }

    /// <summary>
    /// True for a tool that goes back to Look after one click that did something. False for Look
    /// itself and for the tools used in a run.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <returns><c>true</c> for a one-click tool.</returns>
    public static bool IsOneClick(MapTool tool) => tool switch
    {
        MapTool.Reveal => true,
        MapTool.Doors => true,
        MapTool.Conceal => true,
        MapTool.Ping => true,
        _ => false,
    };

    /// <summary>
    /// Puts a tool up. Picking the tool that is already up puts Look back, so a second press of the
    /// same key or button undoes the first.
    /// </summary>
    /// <param name="tool">The tool picked.</param>
    /// <param name="keep">True when Shift was held: a one-click tool then stays up after use.</param>
    public void Pick(MapTool tool, bool keep)
    {
        if (tool == MapTool.None)
        {
            return;
        }

        if (tool == Active && tool != MapTool.Look && keep == Kept)
        {
            Reset();
            return;
        }

        Active = tool;
        Kept = keep && IsOneClick(tool);
    }

    /// <summary>Back to Look: what Esc does.</summary>
    public void Reset()
    {
        Active = MapTool.Look;
        Kept = false;
    }

    /// <summary>
    /// Called after a click on the map that did something. A one-click tool that was not kept gives
    /// way to Look; every other tool stays.
    /// </summary>
    public void Used()
    {
        if (IsOneClick(Active) && !Kept)
        {
            Reset();
        }
    }
}
