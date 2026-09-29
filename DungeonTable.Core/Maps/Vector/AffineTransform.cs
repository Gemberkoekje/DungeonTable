namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// A 2D affine transform in the standard SVG/canvas matrix form
/// (<c>x' = a·x + c·y + e</c>, <c>y' = b·x + d·y + f</c>). Used to place decoration
/// sprites, which carry their own transform rather than baked-in world coordinates.
/// </summary>
/// <param name="A">Scale/rotation term applied to x.</param>
/// <param name="B">Shear/rotation term applied to x.</param>
/// <param name="C">Shear/rotation term applied to y.</param>
/// <param name="D">Scale/rotation term applied to y.</param>
/// <param name="E">X translation.</param>
/// <param name="F">Y translation.</param>
public readonly record struct AffineTransform(double A, double B, double C, double D, double E, double F)
{
    /// <summary>The identity transform (no scale, rotation or translation).</summary>
    public static AffineTransform Identity => new(1, 0, 0, 1, 0, 0);
}
