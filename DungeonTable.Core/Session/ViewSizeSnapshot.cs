namespace DungeonTable.Core.Session;

/// <summary>
/// The size of the projector's view the DM remembered, in grid squares across and down the projector's
/// own screen, as it is saved. Zero across means none is remembered.
/// </summary>
public sealed class ViewSizeSnapshot
{
    /// <summary>Grid squares across the projector's screen, left to right.</summary>
    public double SquaresAcross { get; set; }

    /// <summary>Grid squares down the projector's screen, top to bottom.</summary>
    public double SquaresDown { get; set; }
}
