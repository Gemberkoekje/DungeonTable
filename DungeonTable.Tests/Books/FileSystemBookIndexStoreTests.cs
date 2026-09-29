using System.Collections.Generic;
using System.IO;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Books;

namespace DungeonTable.Tests.Books;

/// <summary>
/// The book index loads from <c>data/book-index</c>: every book, the adventure first, a bad file
/// skipped rather than taking search down, and no directory at all an empty index.
/// </summary>
public sealed class FileSystemBookIndexStoreTests : IDisposable
{
    private static readonly string[] AdventureFirst = { "undercroft", "dmg", "phb" };

    private static readonly string[] FlaggedFirst = { "bestiary", "zine", "almanac", "undercroft" };

    private static readonly string[] OrdinalOrder = { "zine", "almanac" };

    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "dt-books-" + Guid.NewGuid().ToString("N"));

    public FileSystemBookIndexStoreTests() => Directory.CreateDirectory(tempRoot);

    [Fact]
    public void A_book_loads_its_entries_and_links()
    {
        Write("undercroft.json", """
            {
              "book": "undercroft",
              "title": "The Undercroft",
              "entries": [ { "id": "the-old-mill", "kind": "lore", "name": "The Old Mill",
                             "description": "The watermill by the ford.", "page": 6 } ],
              "links": [ { "a": "the-old-mill", "rel": "related", "b": "wenna-brask", "note": "owned and worked by" } ]
            }
            """);

        BookIndex book = Assert.Single(new FileSystemBookIndexStore(tempRoot).AllBooks());

        Assert.Equal("undercroft", book.Book);
        Assert.Equal("The Undercroft", book.Title);
        BookEntry entry = Assert.Single(book.Entries);
        Assert.Equal("the-old-mill", entry.Id);
        Assert.Equal("lore", entry.Kind);
        Assert.Equal("The Old Mill", entry.Name);
        Assert.Equal("The watermill by the ford.", entry.Description);
        Assert.Equal(6, entry.Page);
        Relation link = Assert.Single(book.Links);
        Assert.Equal("the-old-mill", link.A);
        Assert.Equal("related", link.Rel);
        Assert.Equal("wenna-brask", link.B);
        Assert.Equal("owned and worked by", link.Note);
    }

    [Fact]
    public void The_adventure_loads_before_the_rulebooks_whatever_the_file_names()
    {
        // "dmg" and "phb" sort before "undercroft", but the adventure's entry is the one that counts for a
        // shared id.
        Write("phb.json", """{ "book": "phb" }""");
        Write("undercroft.json", """{ "book": "undercroft", "isAdventure": true }""");
        Write("dmg.json", """{ "book": "dmg" }""");

        IReadOnlyList<BookIndex> books = new FileSystemBookIndexStore(tempRoot).AllBooks();

        Assert.Equal(AdventureFirst, books.Select(book => book.Book).ToArray());
        Assert.True(books[0].IsAdventure);
        Assert.False(books[1].IsAdventure);
    }

    [Fact]
    public void A_book_is_the_adventure_because_its_file_says_so_not_because_of_its_name()
    {
        // The app knows no book by name: a file that does not say it is an adventure keeps its
        // file-name place, and two adventures keep file-name order between them.
        Write("almanac.json", """{ "book": "almanac" }""");
        Write("zine.json", """{ "book": "zine", "isAdventure": true }""");
        Write("bestiary.json", """{ "book": "bestiary", "isAdventure": true }""");
        Write("undercroft.json", """{ "book": "undercroft" }""");

        IReadOnlyList<BookIndex> books = new FileSystemBookIndexStore(tempRoot).AllBooks();

        Assert.Equal(FlaggedFirst, books.Select(book => book.Book).ToArray());
    }

    [Fact]
    public void Rulebooks_load_in_ordinal_file_name_order_whatever_the_file_system_lists()
    {
        // Ordinal puts "Zine" before "almanac"; Windows lists them the other way round and Linux in
        // no promised order at all, so the order is decided here rather than by the disk.
        Write("almanac.json", """{ "book": "almanac" }""");
        Write("Zine.json", """{ "book": "zine" }""");

        IReadOnlyList<BookIndex> books = new FileSystemBookIndexStore(tempRoot).AllBooks();

        Assert.Equal(OrdinalOrder, books.Select(book => book.Book).ToArray());
    }

    [Fact]
    public void A_file_that_does_not_parse_is_skipped()
    {
        Write("undercroft.json", """{ "book": "undercroft" }""");
        Write("broken.json", "{ not json");

        Assert.Equal("undercroft", Assert.Single(new FileSystemBookIndexStore(tempRoot).AllBooks()).Book);
    }

    [Fact]
    public void A_null_list_or_a_null_entry_in_a_book_is_read_leniently()
    {
        // The link index and the relations walk every book's entries and links as the DM screen opens,
        // so a hand-edited "entries": null answered every request with an error. A book is read the
        // dossiers' way now: a null list is an empty one and a null entry is dropped.
        Write("undercroft.json", """
            { "book": "undercroft", "entries": [ null, { "id": "the-old-mill", "name": "The Old Mill" } ], "links": null }
            """);
        Write("almanac.json", """
            { "book": "almanac", "entries": null, "links": [ null, { "a": "the-old-mill", "rel": "related", "b": "wenna-brask" } ] }
            """);

        IReadOnlyList<BookIndex> books = new FileSystemBookIndexStore(tempRoot).AllBooks();

        BookIndex undercroft = books.Single(book => book.Book == "undercroft");
        Assert.Equal("the-old-mill", Assert.Single(undercroft.Entries).Id);
        Assert.Empty(undercroft.Links);

        BookIndex almanac = books.Single(book => book.Book == "almanac");
        Assert.Empty(almanac.Entries);
        Assert.Equal("wenna-brask", Assert.Single(almanac.Links).B);
    }

    [Fact]
    public void No_directory_is_an_empty_index()
    {
        Assert.Empty(new FileSystemBookIndexStore(Path.Combine(tempRoot, "missing")).AllBooks());
        Assert.Empty(new FileSystemBookIndexStore(string.Empty).AllBooks());
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private void Write(string name, string json) => File.WriteAllText(Path.Combine(tempRoot, name), json);
}
