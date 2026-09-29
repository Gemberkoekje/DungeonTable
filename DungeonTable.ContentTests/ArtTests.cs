using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Art;

namespace DungeonTable.ContentTests;

/// <summary>
/// The committed art catalogue: every entry is kept, is a picture the art route serves at the size it
/// says, links only stat blocks that exist and can be found; and every committed picture is in it.
/// </summary>
public sealed class ArtTests
{
    public static TheoryData<string> Catalogues => ContentDocuments.Names(DocumentKind.ArtCatalogue);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Catalogues))]
    public void Every_entry_is_kept(string document)
    {
        Report.None(ArtAudit.Dropped(ContentDocuments.Loaded<ArtCatalogue>(document)), $"Entries {document} drops");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Catalogues))]
    public void Every_entry_is_a_picture_the_art_route_serves(string document)
    {
        ArtCatalogue catalogue = ContentDocuments.Loaded<ArtCatalogue>(document);

        Report.None(ArtAudit.Unserved(catalogue, ArtAudit.PicturesOnDisk(ContentRoot.ArtFolder)), $"Entries in {document} with no picture to show");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Catalogues))]
    public void Every_entry_carries_its_pictures_size(string document)
    {
        ArtCatalogue catalogue = ContentDocuments.Loaded<ArtCatalogue>(document);

        Report.None(
            ArtAudit.WrongSizes(catalogue, ContentRoot.ArtFolder, ArtAudit.PicturesOnDisk(ContentRoot.ArtFolder)),
            $"Entries in {document} with the wrong size");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Catalogues))]
    public void Every_node_an_entry_links_is_a_stat_block_the_library_has(string document)
    {
        Report.None(ArtAudit.UnknownNodes(ContentDocuments.Loaded<ArtCatalogue>(document), ContentRoot.Stats), $"Links in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Catalogues))]
    public void Every_entry_can_be_shown_and_found(string document)
    {
        Report.None(ArtAudit.Thin(ContentDocuments.Loaded<ArtCatalogue>(document)), $"Entries in {document}");
    }

    [Fact]
    public void Every_committed_picture_is_in_the_catalogue()
    {
        // A fact rather than a theory over the catalogue: pictures with no catalogue at all are orphans too.
        IReadOnlyList<ContentDocument> catalogues = ContentDocuments.Of(DocumentKind.ArtCatalogue);
        ArtCatalogue catalogue = catalogues.Count > 0
            ? ContentDocuments.Loaded<ArtCatalogue>(catalogues[0].Name)
            : new ArtCatalogue();

        Report.None(ArtAudit.Orphans(catalogue, ArtAudit.PicturesOnDisk(ContentRoot.ArtFolder)), "Pictures nobody can show");
    }
}
