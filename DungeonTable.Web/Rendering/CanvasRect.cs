namespace DungeonTable.Web.Rendering;

/// <summary>
/// A rectangle dragged out on the map canvas in the Room Editor, in world units, used to create a
/// rectangular region.
/// </summary>
/// <param name="X">Left edge in world units.</param>
/// <param name="Y">Top edge in world units.</param>
/// <param name="Width">Width in world units.</param>
/// <param name="Height">Height in world units.</param>
public readonly record struct CanvasRect(double X, double Y, double Width, double Height);
