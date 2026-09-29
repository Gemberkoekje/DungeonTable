namespace DungeonTable.Web.Rendering;

/// <summary>
/// A single fog-brush gesture reported from the map input layer, in world units. The DM view snaps
/// it to a grid cell before updating the reveal state.
/// </summary>
/// <param name="WorldX">Pointer X in map world units.</param>
/// <param name="WorldY">Pointer Y in map world units.</param>
/// <param name="Erase">True when the gesture hides cells again (modifier / secondary button).</param>
public readonly record struct FogPaint(double WorldX, double WorldY, bool Erase);
