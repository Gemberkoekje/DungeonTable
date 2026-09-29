using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.ContentTests.Rules;

namespace DungeonTable.ContentTests;

/// <summary>
/// Every document loads, nothing among the documents is a file the app never reads, and no list in a
/// document holds a <c>null</c>. The app skips all three without a word, so the table stays up; these
/// are where they are said out loud.
/// </summary>
public sealed class DocumentTests
{
    public static TheoryData<string> Documents => ContentDocuments.AllNames();

    // The documents whose store drops a null entry in a list (LenientListConverter): every one the app
    // reads as JSON, a map's notes and the rosters included.
    public static TheoryData<string> LenientDocuments => ContentDocuments.Names(
        DocumentKind.Level, DocumentKind.Reference, DocumentKind.Campaign, DocumentKind.Characters, DocumentKind.Npcs,
        DocumentKind.Story, DocumentKind.Sessions, DocumentKind.Relations, DocumentKind.Quests, DocumentKind.Deck,
        DocumentKind.Monsters, DocumentKind.Spells, DocumentKind.BookIndex, DocumentKind.ArtCatalogue,
        DocumentKind.MapRegions, DocumentKind.PartyRoster, DocumentKind.AllyRoster);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Documents))]
    public async Task Every_document_loads(string document)
    {
        string fault = await DocumentAudit.LoadFaultAsync(ContentDocuments.Named(document));

        Assert.True(fault.Length == 0, $"{document} does not load, so the app skips it: {fault}");
    }

    [Fact]
    public void Every_json_file_among_the_documents_is_one_the_app_reads()
    {
        Report.None(DocumentAudit.UnreadFiles(ContentDocuments.Unread), "Files the app never reads");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(LenientDocuments))]
    public async Task No_list_in_a_document_holds_a_null(string document)
    {
        ContentDocument named = ContentDocuments.Named(document);
        string loadFault = await DocumentAudit.LoadFaultAsync(named);
        Assert.SkipUnless(loadFault.Length == 0, $"{document} does not load, which Every_document_loads reports.");

        string json = await File.ReadAllTextAsync(named.FullPath, CancellationToken.None);
        Report.None(DocumentAudit.NullEntries(json), $"Null entries in {document}");
    }
}
