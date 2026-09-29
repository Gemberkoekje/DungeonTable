using System.IO;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests;

/// <summary>
/// Every level file: the app can place its floor and every area on it, nothing in it is written twice,
/// and every area can be reached, by a link and from the DM panel.
/// </summary>
public sealed class LevelTests
{
    public static TheoryData<string> LevelFiles => ContentDocuments.Names(DocumentKind.Level);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(LevelFiles))]
    public void Every_level_file_places_its_floor_and_its_areas(string document)
    {
        LevelDossierSet set = ContentDocuments.Loaded<LevelDossierSet>(document);

        Report.None(LevelAudit.Unplaced(Path.GetFileName(document), set), $"What {document} leaves unplaced");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(LevelFiles))]
    public void Nothing_in_a_level_file_is_written_twice(string document)
    {
        LevelDossierSet set = ContentDocuments.Loaded<LevelDossierSet>(document);

        Report.None(
            LevelAudit.WrittenTwice(document, set, ContentDocuments.LoadedOf<LevelDossierSet>(DocumentKind.Level)),
            $"Ids in {document} that are written twice");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(LevelFiles))]
    public void Every_area_is_reached_by_its_printed_key(string document)
    {
        LevelDossierSet set = ContentDocuments.Loaded<LevelDossierSet>(document);

        Report.None(LevelAudit.Unreachable(set, ContentRoot.Targets), $"Areas in {document} that no link reaches");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(LevelFiles))]
    public void Every_area_opens_a_briefing(string document)
    {
        LevelDossierSet set = ContentDocuments.Loaded<LevelDossierSet>(document);

        Report.None(LevelAudit.Unbriefed(set, ContentRoot.Projection), $"Areas in {document} with no briefing");
    }
}
