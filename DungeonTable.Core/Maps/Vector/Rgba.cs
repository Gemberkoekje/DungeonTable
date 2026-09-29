namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// An immutable RGBA colour. Dungeon Scrawl stores a colour as a packed <c>0xRRGGBB</c>
/// integer plus a separate 0..1 opacity; <see cref="FromPacked"/> decodes that pair.
/// </summary>
/// <param name="R">Red channel (0-255).</param>
/// <param name="G">Green channel (0-255).</param>
/// <param name="B">Blue channel (0-255).</param>
/// <param name="A">Alpha channel (0-255).</param>
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    /// <summary>
    /// Decodes a Dungeon Scrawl colour: <paramref name="packed"/> is a decimal integer
    /// encoding <c>0xRRGGBB</c> (not a hex string, not ARGB), and <paramref name="alpha"/>
    /// is the separate 0..1 opacity. Note <c>0</c> means opaque black, not "unset".
    /// </summary>
    /// <param name="packed">The packed <c>0xRRGGBB</c> colour value.</param>
    /// <param name="alpha">Opacity in the 0..1 range.</param>
    /// <returns>The decoded colour.</returns>
    public static Rgba FromPacked(int packed, double alpha)
    {
        byte r = (byte)((packed >> 16) & 0xFF);
        byte g = (byte)((packed >> 8) & 0xFF);
        byte b = (byte)(packed & 0xFF);
        byte a = (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255);
        return new Rgba(r, g, b, a);
    }

    /// <summary>Renders the colour as a <c>#RRGGBB</c> hex string for SVG output.</summary>
    /// <returns>The <c>#RRGGBB</c> hex representation.</returns>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
}
