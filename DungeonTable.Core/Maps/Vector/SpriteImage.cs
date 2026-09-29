namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// A decoration sprite embedded in the source file (a small raster icon such as a statue,
/// pillar or rubble). Shared by every <see cref="MapDecoration"/> that references the same
/// <see cref="AssetId"/>, so it is stored once and placed many times.
/// <see cref="Width"/> and <see cref="Height"/> are the sprite's size in world units; the
/// placement transform maps the sprite's top-left corner (its local origin).
/// </summary>
public sealed class SpriteImage
{
    /// <summary>Source asset id that decorations reference.</summary>
    public string AssetId { get; init; } = string.Empty;

    /// <summary>Self-contained <c>data:</c> URI (the embedded image, correct MIME).</summary>
    public string DataUri { get; init; } = string.Empty;

    /// <summary>Sprite width in world units.</summary>
    public double Width { get; init; }

    /// <summary>Sprite height in world units.</summary>
    public double Height { get; init; }
}
