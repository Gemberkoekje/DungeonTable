using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Books;
using DungeonTable.Infrastructure.Dossiers;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Infrastructure.Stats;

namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// One consistent reading of the content: the dossiers, the stat library and the book index, and
/// everything built from them together (the link index, the projection behind briefings, cards and
/// search, and the prose link resolver with its cache). Immutable, so any number of circuits read it
/// at once. A reload builds a whole new snapshot and swaps it in; nothing ever changes inside one.
/// </summary>
public sealed class ContentSnapshot
{
    /// <summary>Builds everything that depends on the three stores.</summary>
    /// <param name="dossiers">The dossiers, read.</param>
    /// <param name="stats">The stat library, read.</param>
    /// <param name="books">The book index, read.</param>
    public ContentSnapshot(FileSystemDossierStore dossiers, FileSystemStatLibrary stats, FileSystemBookIndexStore books)
    {
        ArgumentNullException.ThrowIfNull(dossiers);
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(books);

        Dossiers = dossiers;
        Stats = stats;
        Books = books;

        IReadOnlyList<BookIndex> all = books.AllBooks();
        Targets = LinkTargets.Build(dossiers, stats, all);
        Projection = new AuthoredProjection(dossiers, stats, Targets, all);
        Resolver = new MarkupCrossRefResolver(Targets);
        Problems = dossiers.Problems.Concat(stats.Problems).Concat(books.Problems).ToList();
    }

    /// <summary>The dossiers.</summary>
    public FileSystemDossierStore Dossiers { get; }

    /// <summary>The monsters and spells.</summary>
    public FileSystemStatLibrary Stats { get; }

    /// <summary>The book index.</summary>
    public FileSystemBookIndexStore Books { get; }

    /// <summary>Everything a link can name.</summary>
    public LinkTargets Targets { get; }

    /// <summary>Briefings, reference cards and search.</summary>
    public AuthoredProjection Projection { get; }

    /// <summary>The prose link resolver.</summary>
    public MarkupCrossRefResolver Resolver { get; }

    /// <summary>What the three stores skipped or dropped reading this snapshot's documents.</summary>
    public IReadOnlyList<ContentProblem> Problems { get; }

    /// <summary>Reads the documents in three folders into a snapshot.</summary>
    /// <param name="dossiersRoot">The dossiers folder.</param>
    /// <param name="statsRoot">The stat block folder.</param>
    /// <param name="bookIndexRoot">The book index folder; empty when the content has none.</param>
    /// <returns>The snapshot.</returns>
    public static ContentSnapshot Read(string dossiersRoot, string statsRoot, string bookIndexRoot) =>
        new ContentSnapshot(
            new FileSystemDossierStore(dossiersRoot),
            new FileSystemStatLibrary(statsRoot),
            new FileSystemBookIndexStore(bookIndexRoot));
}
