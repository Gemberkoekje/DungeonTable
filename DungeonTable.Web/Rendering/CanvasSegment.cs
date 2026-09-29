namespace DungeonTable.Web.Rendering;

/// <summary>
/// A line dragged out on the map canvas, in world units — the measuring tool's gesture. Unlike
/// <see cref="CanvasRect"/> this keeps the two ends as they were drawn rather than normalizing them
/// into a rectangle, so the line is drawn back exactly where the DM put it.
/// </summary>
/// <param name="FromX">Where the drag started, X in world units.</param>
/// <param name="FromY">Where the drag started, Y in world units.</param>
/// <param name="ToX">Where the drag ended, X in world units.</param>
/// <param name="ToY">Where the drag ended, Y in world units.</param>
public readonly record struct CanvasSegment(double FromX, double FromY, double ToX, double ToY);
