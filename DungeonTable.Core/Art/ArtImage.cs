namespace DungeonTable.Core.Art;

/// <summary>
/// One catalogued picture the DM can push onto the player projector: the file to serve, what it
/// shows, and how to find it again.
/// </summary>
/// <remarks>
/// <see cref="Shows"/> is the load-bearing field. It links a picture to what it depicts, named the way
/// a link names it, so a mention of Wenna Brask anywhere can put her portrait on the projector, and
/// "suggest art for the creatures in the running fight" is a lookup rather than a fuzzy name match. It
/// is deliberately a <em>list</em>: one picture can serve several things (the Bell-Warden is also what
/// any gargoyle looks like), and the first it names is what it is most a picture of.
/// <see cref="NodeIds"/> is the older form, stat blocks by node id, still read.
/// </remarks>
public sealed class ArtImage
{
    /// <summary>Stable id, unique across the catalogue ("demo-bell-warden").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Path relative to the art root, in URL form ("demo/bell-warden.webp"). Served under
    /// <c>/art/</c>, which sits below the passphrase gate like every other route.
    /// </summary>
    public string File { get; init; } = string.Empty;

    /// <summary>The name shown in the picker ("The Bell-Warden").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>One line describing what is in the picture, in the words a DM would search for.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>What the picture depicts.</summary>
    public ArtKind Kind { get; init; }

    /// <summary>Whether this is committed art or a DM upload, set from the catalogue the entry is read from.</summary>
    public ArtOrigin Origin { get; init; }

    /// <summary>The book it was taken from ("Bestiary.pdf"); empty for original art and for an upload.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>The printed page it was taken from ("p.12"); empty for original art and for an upload.</summary>
    public string SourceLocation { get; init; } = string.Empty;

    /// <summary>Free search terms — creature type, mood, gang ("construct", "gargoyle", "statue").</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// What this picture depicts, each named the way a link names it: a person or an entity by id
    /// ("wenna-brask", "bell-warden"), a stat block by name ("gargoyle"), a book entry by id, an area
    /// with its floor ("L1 area 3"). The first is what it is most a picture of. Empty when it names none.
    /// </summary>
    public IReadOnlyList<string> Shows { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Node ids of the stat blocks this picture depicts: the older form of <see cref="Shows"/>, still
    /// read, and counted after it. Empty when it names none.
    /// </summary>
    public IReadOnlyList<string> NodeIds { get; init; } = Array.Empty<string>();

    /// <summary>Pixel width of the stored file, so the overlay can size its frame before the image loads.</summary>
    public int Width { get; init; }

    /// <summary>Pixel height of the stored file.</summary>
    public int Height { get; init; }
}
