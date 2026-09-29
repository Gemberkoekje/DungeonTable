using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests;

/// <summary>
/// The campaign dossier: the DM screen opens on an area that exists, and the level table says what each
/// level is and which of them the app has.
/// </summary>
public sealed class CampaignTests
{
    public static TheoryData<string> Campaigns => ContentDocuments.Names(DocumentKind.Campaign);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Campaigns))]
    public void The_start_area_is_one_a_level_file_writes(string document)
    {
        CampaignDossier campaign = ContentDocuments.Loaded<CampaignDossier>(document);

        Report.None(CampaignAudit.MissingStartArea(campaign, ContentRoot.Dossiers), "The start area");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Campaigns))]
    public void Every_level_row_says_what_the_level_is(string document)
    {
        CampaignDossier campaign = ContentDocuments.Loaded<CampaignDossier>(document);

        Report.None(CampaignAudit.IncompleteLevelRows(campaign), $"Level rows in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Campaigns))]
    public void The_level_table_marks_as_authored_exactly_the_floors_with_a_level_file(string document)
    {
        CampaignDossier campaign = ContentDocuments.Loaded<CampaignDossier>(document);

        Report.None(
            CampaignAudit.WrongAuthoredMarks(campaign, CampaignAudit.FloorsWithAFile(ContentRoot.Dossiers)),
            $"Level rows in {document} whose authored mark is wrong");
    }
}
