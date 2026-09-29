namespace DungeonTable.Core.Art;

/// <summary>
/// What an uploaded picture has to be before the app will touch it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No SVG, and no trusting the file name.</b> The <c>/maps</c> route allows <c>image/svg+xml</c>,
/// which is right for a map the DM authored themselves. It is wrong here: an SVG fetched directly at
/// <c>/art/x.svg</c> is same-origin markup and can run script, so an upload route that accepts one
/// hands anyone who can reach the DM screen a stored-XSS primitive on the app's own origin. Raster
/// only, and decided by the bytes rather than the extension the browser reported.
/// </para>
/// <para>
/// The sniff is a gate, not the validation. It rejects the obviously-wrong file cheaply and with a
/// message the DM can act on; the <em>real</em> check is that the decoder can read the file, which
/// happens next and is what makes the re-encode safe.
/// </para>
/// <para>
/// <b><see cref="MaxBytes"/> alone does not bound the work.</b> A compressed image's byte size says
/// nothing about how much memory decoding it costs — a near-solid-colour PNG declaring 30000x30000
/// compresses to a few KB and still wants gigabytes of pixel buffer. That is why
/// <see cref="MaxPixels"/> exists and why the adapter reads the header dimensions before it decodes
/// anything.
/// </para>
/// </remarks>
public static class ArtUploadRules
{
    /// <summary>The largest upload accepted, in bytes. A book-page scan is comfortably under this.</summary>
    public const long MaxBytes = 12L * 1024 * 1024;

    /// <summary>
    /// The most pixels an upload may declare, checked from the header <em>before</em> decoding.
    /// </summary>
    /// <remarks>
    /// 50 megapixels: comfortably above a 600-dpi A4 page scan (~35 MP) and a 48 MP phone photo,
    /// far below the point where the decode allocation threatens the one shared process the whole
    /// table depends on. The bound is on the pixel count rather than either edge because it is the
    /// product that drives the allocation.
    /// </remarks>
    public const long MaxPixels = 50L * 1000 * 1000;

    /// <summary>
    /// The most uploads kept at once. The art directory is a bind mount shared with the committed
    /// book art, so an unbounded upload pile is somebody's disk.
    /// </summary>
    public const int MaxUploads = 60;

    /// <summary>Longest edge of a stored upload, in pixels.</summary>
    public const int MaxEdgePixels = 1400;

    /// <summary>Human-readable list of what is accepted, for the upload control and its errors.</summary>
    public const string AcceptedFormats = "PNG, JPEG or WebP";

    /// <summary>
    /// Decides whether the bytes start with a raster header this app accepts.
    /// </summary>
    /// <param name="content">The uploaded bytes.</param>
    /// <returns><c>true</c> when the content begins with a PNG, JPEG or WebP signature.</returns>
    public static bool IsAcceptedRaster(ReadOnlySpan<byte> content) =>
        IsPng(content) || IsJpeg(content) || IsWebp(content);

    // 89 'P' 'N' 'G' CR LF SUB LF
    private static bool IsPng(ReadOnlySpan<byte> content) =>
        content.Length >= 8
        && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47
        && content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A;

    // FF D8 FF: start of image, then the first marker.
    private static bool IsJpeg(ReadOnlySpan<byte> content) =>
        content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF;

    // "RIFF" <4-byte length> "WEBP". The length in between is why this is not one contiguous compare.
    private static bool IsWebp(ReadOnlySpan<byte> content) =>
        content.Length >= 12
        && content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
        && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P';
}
