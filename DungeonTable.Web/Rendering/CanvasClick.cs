namespace DungeonTable.Web.Rendering;

/// <summary>
/// A click reported from the map canvas in the Room Editor: the world-unit coordinate plus what,
/// if anything, was under the pointer (a region, feature marker, or a door/stair object).
/// </summary>
/// <param name="WorldX">Click X in map world units.</param>
/// <param name="WorldY">Click Y in map world units.</param>
/// <param name="HitKind">"region", "feature", "door", "stairs", or an empty string for no hit.</param>
/// <param name="HitId">Id of the hit element, or an empty string.</param>
public readonly record struct CanvasClick(double WorldX, double WorldY, string HitKind, string HitId);
