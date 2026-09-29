namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// Hand-drawn "wobble" preset applied to a stroke, from the string <c>roughOptions</c>
/// found on wall strokes and shadows in a Dungeon Scrawl file.
/// </summary>
public enum RoughLevel
{
    /// <summary>Unset / crisp (no roughening).</summary>
    None = 0,

    /// <summary>Light roughening (<c>"low"</c>).</summary>
    Low,

    /// <summary>Heavy roughening (<c>"high"</c>).</summary>
    High,
}
