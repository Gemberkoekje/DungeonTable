namespace DungeonTable.Core.Art;

/// <summary>
/// How a picture is turned and flipped on the projector: a scan that came in on its side, a portrait
/// facing the wrong way, or a tall picture turned to fill a wide screen.
/// </summary>
/// <param name="QuarterTurns">Quarter turns clockwise, from 0 to 3.</param>
/// <param name="Mirrored">True when it is flipped left to right, before it is turned.</param>
public readonly record struct ArtOrientation(int QuarterTurns, bool Mirrored)
{
    /// <summary>As the picture is: upright and not flipped.</summary>
    public static ArtOrientation Upright => default;

    /// <summary>True when it is upright and not flipped.</summary>
    public bool IsUpright => QuarterTurns == 0 && !Mirrored;

    /// <summary>True when it lies on its side: a quarter or three quarters turned.</summary>
    public bool OnItsSide => QuarterTurns % 2 == 1;

    /// <summary>The angle it is turned clockwise, in degrees.</summary>
    public int Degrees => QuarterTurns * 90;

    /// <summary>The same picture a quarter turn further clockwise.</summary>
    /// <returns>The turned orientation.</returns>
    public ArtOrientation Turned() => this with { QuarterTurns = Normalised(QuarterTurns + 1) };

    /// <summary>The same picture flipped left to right, or flipped back.</summary>
    /// <returns>The flipped orientation.</returns>
    public ArtOrientation Flipped() => this with { Mirrored = !Mirrored };

    /// <summary>Any count of quarter turns as one from 0 to 3; a negative count turns the other way.</summary>
    /// <param name="quarterTurns">The quarter turns.</param>
    /// <returns>The same turn, from 0 to 3.</returns>
    public static int Normalised(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;
}
