using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests;

/// <summary>Every book's index: its entries answer to their own ids, and its links can be drawn.</summary>
public sealed class BookIndexTests
{
    public static TheoryData<string> Books => ContentDocuments.Names(DocumentKind.BookIndex);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Books))]
    public void Every_entry_answers_to_its_own_id(string document)
    {
        BookIndex book = ContentDocuments.Loaded<BookIndex>(document);

        Report.None(LinkAudit.BadBookEntries(new[] { book }, ContentRoot.Targets), $"Unusable entries in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Books))]
    public void Every_link_can_be_drawn(string document)
    {
        BookIndex book = ContentDocuments.Loaded<BookIndex>(document);

        Report.None(LinkAudit.BrokenBookLinks(new[] { book }, ContentRoot.Targets), $"Links in {document} that cannot be drawn");
    }
}
