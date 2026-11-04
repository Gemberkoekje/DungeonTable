namespace DungeonTable.Web.Services;

/// <summary>
/// A size of the projector's view in grid squares, across and down the projector's own screen: what
/// keeps a square on the table the same size from one map to the next.
/// </summary>
/// <param name="Across">Grid squares across the screen, left to right.</param>
/// <param name="Down">Grid squares down the screen, top to bottom.</param>
public readonly record struct ViewSize(double Across, double Down)
{
    /// <summary>No size: nothing remembered.</summary>
    public static ViewSize None => default;

    /// <summary>True when both sides are a usable number of squares.</summary>
    public bool IsSet => double.IsFinite(Across) && double.IsFinite(Down) && Across > 0 && Down > 0;
}
