using System.Diagnostics;
using System.Threading;

namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// The content the table is showing, and the one place it is read again when a document changes on
/// disk. Holds the current <see cref="ContentSnapshot"/>; the stores the pages see
/// (<see cref="LiveDossierStore"/> and its siblings) hand every call on to it, so a reload changes
/// what every page sees in one step without any of them being rebuilt.
/// </summary>
/// <remarks>
/// <para>
/// A JSON file edited by hand or by an LLM is part-way wrong as often as not, and an editor saving in
/// two writes can be caught between them. So a reload that would lose a document the table can read
/// now (it no longer parses) is not taken: the table keeps the content it had, and the reload says
/// which file and why. A document that could not be read before either does not hold a reload up.
/// </para>
/// <para>
/// Thread-safe: reloads are serialized, and the snapshot is swapped with a single reference write, so
/// a page in the middle of a render sees either the old content or the new, never a mixture of one
/// store from each. <see cref="Changed"/> is raised outside the lock, on the reloading thread; a page
/// that handles it marshals onto its own dispatcher.
/// </para>
/// </remarks>
public sealed class LiveContent
{
    private readonly string dossiersRoot;
    private readonly string statsRoot;
    private readonly string bookIndexRoot;
    private readonly object reloading = new object();
    private ContentSnapshot current;

    /// <summary>Reads the content for the first time.</summary>
    /// <param name="dossiersRoot">The dossiers folder.</param>
    /// <param name="statsRoot">The stat block folder.</param>
    /// <param name="bookIndexRoot">The book index folder; empty when the content has none.</param>
    public LiveContent(string dossiersRoot, string statsRoot, string bookIndexRoot)
    {
        this.dossiersRoot = dossiersRoot;
        this.statsRoot = statsRoot;
        this.bookIndexRoot = bookIndexRoot ?? string.Empty;
        current = ContentSnapshot.Read(dossiersRoot, statsRoot, this.bookIndexRoot);
    }

    /// <summary>Raised after the content changed: a new snapshot, or a map or the art catalogue on disk.</summary>
    public event Action<ContentChanges> Changed;

    /// <summary>The content the table is showing now.</summary>
    public ContentSnapshot Current => Volatile.Read(ref current);

    /// <summary>
    /// Reads the dossiers, the stat library and the book index again, and shows the result, unless a
    /// document that is read now could not be read any more.
    /// </summary>
    /// <returns>What the reload did.</returns>
    public ContentReload ReloadDocuments()
    {
        ContentReload outcome;
        lock (reloading)
        {
            var clock = Stopwatch.StartNew();
            ContentSnapshot next = ContentSnapshot.Read(dossiersRoot, statsRoot, bookIndexRoot);
            clock.Stop();

            var unreadableBefore = new HashSet<string>(
                Current.Problems.Where(problem => problem.DocumentSkipped).Select(problem => problem.File),
                StringComparer.Ordinal);
            List<ContentProblem> blocking = next.Problems
                .Where(problem => problem.DocumentSkipped && !unreadableBefore.Contains(problem.File))
                .ToList();
            if (blocking.Count > 0)
            {
                return new ContentReload(false, next.Problems, blocking, clock.Elapsed);
            }

            Volatile.Write(ref current, next);
            outcome = new ContentReload(true, next.Problems, Array.Empty<ContentProblem>(), clock.Elapsed);
        }

        Changed?.Invoke(ContentChanges.Documents);
        return outcome;
    }

    /// <summary>
    /// Tells the pages that something outside the snapshot changed on disk (a map, the art catalogue),
    /// so each reads what it shows of it again.
    /// </summary>
    /// <param name="changes">What changed.</param>
    public void Announce(ContentChanges changes)
    {
        if (changes != ContentChanges.None)
        {
            Changed?.Invoke(changes);
        }
    }
}
