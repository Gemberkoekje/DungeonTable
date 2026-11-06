using System.Collections.Generic;
using System.Text;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// The DM's fog layer in the pieces a canvas sends one at a time: the reveal mask's frame, what it
/// holds besides the revealed squares (its white sheet and the revealed rooms), the revealed squares in
/// bands of rows, and the fog sheet drawn through the mask with the revealed markers over it.
/// </summary>
/// <remarks>
/// Blazor sends a piece of markup again whole when one character of it changes. The revealed squares
/// used to be one path, so revealing one square sent every square revealed all evening again, on every
/// step of the party token. In bands, a step sends only the bands it touched. There is a band for every
/// <see cref="VectorMapSvgRenderer.FogRowsPerBand"/> rows of the map, revealed or not, so the list keeps
/// its length and each band its place from one render to the next.
/// </remarks>
public sealed class FogLayerParts
{
    /// <summary>No fog: nothing is drawn.</summary>
    public static FogLayerParts None { get; } = new FogLayerParts();

    /// <summary>True when there is a fog layer to draw.</summary>
    public bool Enabled { get; init; }

    /// <summary>The mask's <c>id</c>, which the fog sheet refers to.</summary>
    public string MaskId { get; init; } = string.Empty;

    /// <summary>The mask's left edge, as written in its <c>x</c> attribute.</summary>
    public string X { get; init; } = string.Empty;

    /// <summary>The mask's top edge, as written in its <c>y</c> attribute.</summary>
    public string Y { get; init; } = string.Empty;

    /// <summary>The mask's width, as written in its <c>width</c> attribute.</summary>
    public string Width { get; init; } = string.Empty;

    /// <summary>The mask's height, as written in its <c>height</c> attribute.</summary>
    public string Height { get; init; } = string.Empty;

    /// <summary>The mask's white sheet and the revealed rooms punched out of it.</summary>
    public string Shapes { get; init; } = string.Empty;

    /// <summary>The revealed squares, one piece per band of rows; an empty string for a band with none.</summary>
    public IReadOnlyList<string> Bands { get; init; } = Array.Empty<string>();

    /// <summary>After the mask: the fog sheet drawn through it, and the revealed markers.</summary>
    public string Sheet { get; init; } = string.Empty;

    /// <summary>The layer as one piece of markup, as <see cref="VectorMapSvgRenderer.Render"/> draws it.</summary>
    /// <returns>The markup; empty when there is no fog.</returns>
    public string ToMarkup()
    {
        if (!Enabled)
        {
            return string.Empty;
        }

        var svg = new StringBuilder(Shapes.Length + Sheet.Length + 256);
        svg.Append("<mask id=\"").Append(MaskId).Append("\" maskUnits=\"userSpaceOnUse\" x=\"").Append(X)
            .Append("\" y=\"").Append(Y).Append("\" width=\"").Append(Width).Append("\" height=\"").Append(Height)
            .Append("\">").Append(Shapes);
        foreach (string band in Bands)
        {
            svg.Append(band);
        }

        return svg.Append("</mask>").Append(Sheet).ToString();
    }
}
