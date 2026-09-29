namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// A placed decoration object (statue, pillar, rubble, rock, pit, …): a reference to a
/// shared <see cref="SpriteImage"/> plus the <see cref="Transform"/> that positions and
/// scales it. Like doors and stairs, this carries no interactive state of its own — it is
/// scenery — but it has a stable <see cref="Id"/>, so the Room Editor can annotate an
/// individual placement (for example to conceal it from the players).
/// </summary>
public sealed class MapDecoration
{
    /// <summary>Source object id, stable across loads of the same file.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Source object name (for example <c>Statue1x1</c>); scenery, not gameplay state.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Asset id of the sprite this decoration draws (see <see cref="SpriteImage.AssetId"/>).</summary>
    public string AssetId { get; init; } = string.Empty;

    /// <summary>Placement transform; maps the sprite's top-left corner (its local origin) into world space.</summary>
    public AffineTransform Transform { get; init; } = AffineTransform.Identity;

    /// <summary>
    /// World-unit bounding box of the placed sprite (its local box run through
    /// <see cref="Transform"/>). Carried like <see cref="DoorFeature.Bounds"/> so the editor and the
    /// DM view can hit-test a placement without re-deriving it from the sprite table.
    /// </summary>
    public MapBounds Bounds { get; init; }

    /// <summary>Opacity in the 0..1 range.</summary>
    public double Alpha { get; init; } = 1;
}
