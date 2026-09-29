using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Content;
using DungeonTable.Infrastructure.Dossiers;

namespace DungeonTable.Infrastructure.Books;

/// <summary>
/// File-system adapter for <see cref="IBookIndexStore"/>: reads every <c>*.json</c> in the book index
/// directory once, at construction. Immutable afterwards, so it is safe to share as a singleton.
/// </summary>
/// <remarks>
/// <para>
/// Books load in file-name order, except that a book that says it is an adventure
/// (<see cref="BookIndex.IsAdventure"/>) comes first: where two books have an entry with the same id,
/// the adventure's is the one that counts.
/// </para>
/// <para>
/// A file that does not parse is skipped, like an unreadable dossier document, so one bad book cannot
/// take search down; the content checks load every file and would fail on it. In one that does parse,
/// a <c>null</c> list is read as empty and a <c>null</c> entry is dropped, as in the dossiers: the link
/// index and the relations walk every book's entries and links as the DM screen opens.
/// </para>
/// </remarks>
public sealed class FileSystemBookIndexStore : IBookIndexStore
{
    // Internal so the content tests can read every book the way this store does, and report one it
    // would skip.
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,

        // A null reads as if the field were left out, as in the dossiers.
        TypeInfoResolver = NullMeansLeftOut.Resolver(),
        Converters = { new NullTolerantValueConverter(), new LenientListConverter(), new LenientStringConverter() },
    };

    private readonly IReadOnlyList<BookIndex> books;
    private readonly List<ContentProblem> problems = new List<ContentProblem>();

    /// <summary>Loads the book index.</summary>
    /// <param name="root">The book index directory; an empty or missing one gives an empty index.</param>
    public FileSystemBookIndexStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            books = Array.Empty<BookIndex>();
            return;
        }

        // The flag is inside the file, so the books are read in file-name order first and moved after.
        // A stable sort, so each group keeps that order.
        books = Directory
            .EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Select(Read)
            .Where(book => book is not null)
            .OrderBy(book => book.IsAdventure ? 0 : 1)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<BookIndex> AllBooks() => books;

    /// <summary>
    /// What the store skipped or dropped while reading the books, file by file: a book that does not
    /// parse, and a <c>null</c> entry. Empty when every book loaded.
    /// </summary>
    public IReadOnlyList<ContentProblem> Problems => problems;

    // One book, or null when the file cannot be read or parsed, which is reported.
    private BookIndex Read(string path)
    {
        string reason;
        try
        {
            string json = File.ReadAllText(path);
            BookIndex book = JsonSerializer.Deserialize<BookIndex>(json, Options);
            if (book is not null)
            {
                problems.AddRange(NullEntries.Described(json).Select(entry => new ContentProblem(path, entry)));
                return book;
            }

            reason = "it holds nothing but null";
        }
        catch (JsonException error)
        {
            reason = error.Message;
        }
        catch (IOException error)
        {
            reason = error.Message;
        }
        catch (UnauthorizedAccessException error)
        {
            reason = error.Message;
        }

        problems.Add(ContentProblem.Unreadable(path, reason));
        return null;
    }
}
