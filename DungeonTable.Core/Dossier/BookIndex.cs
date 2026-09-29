namespace DungeonTable.Core.Dossier;

/// <summary>
/// One book's index, <c>data/book-index/&lt;book&gt;.json</c>: what the book mentions, and how those
/// mentions link, so search reaches the whole book.
/// </summary>
/// <remarks>
/// <para>
/// An index is often machine-made from the book, so its links are not trusted with a kind: every link
/// is <see cref="RelationKinds.Related"/>, with the book's own wording as its note. A relation the
/// campaign vouches for belongs in its own content, with one of the authored kinds.
/// </para>
/// <para>
/// A link's ends are named the way any link target is: another entry's id, an authored id
/// (<c>wenna-brask</c>), a stat block (<c>goblin</c>) or an area on a numbered floor (<c>L1 area 1</c>).
/// </para>
/// </remarks>
public sealed class BookIndex
{
    /// <summary>The book's short id ("almanac", "srd"), which is also its file name.</summary>
    public string Book { get; init; } = string.Empty;

    /// <summary>The book's title ("A Millbrook Almanac"), for citations.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The source file the book was extracted from ("SRD_CC_v5.1.pdf"): what a stat block, a spell or a
    /// picture names as its <c>source</c>, so their citations can show the book's
    /// <see cref="Title"/> instead. Empty when unknown.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// True for the adventure the campaign runs, rather than a rulebook. An adventure's book loads
    /// first, so where two books have an entry under the same id, the adventure's is the one that
    /// counts.
    /// </summary>
    public bool IsAdventure { get; init; }

    /// <summary>The things the book mentions.</summary>
    public IReadOnlyList<BookEntry> Entries { get; init; } = Array.Empty<BookEntry>();

    /// <summary>How they link, to each other and to the campaign's own content.</summary>
    public IReadOnlyList<Relation> Links { get; init; } = Array.Empty<Relation>();
}
