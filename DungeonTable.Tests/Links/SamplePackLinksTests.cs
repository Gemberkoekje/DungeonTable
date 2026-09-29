using System.Collections.Generic;
using System.IO;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Books;
using DungeonTable.Infrastructure.Dossiers;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Infrastructure.Stats;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Links;

/// <summary>
/// Links, search and citations run on real files: the sample pack, loaded the way the app loads it.
/// These check the app's logic on content it did not write itself; the rules any content has to keep
/// are the content tests' (DungeonTable.ContentTests).
/// </summary>
public sealed class SamplePackLinksTests
{
    private const string Level1 = "data_dossiers_level_1";
    private const string LowerNave = "data_dossiers_level_1_area_3";
    private const string Nave = "data_dossiers_level_1_area_3a";
    private const string Vestry = "data_dossiers_level_1_area_3b";
    private const string Workshop = "data_dossiers_level_1_area_4";

    private static readonly string[] NaveAndVestry = { Nave, Vestry };

    private static readonly FileSystemDossierStore Dossiers =
        new(Path.Combine(SamplePack.Root, "data", "dossiers"));

    private static readonly FileSystemStatLibrary Stats =
        new(Path.Combine(SamplePack.Root, "data", "statblocks"));

    private static readonly IReadOnlyList<BookIndex> Books =
        new FileSystemBookIndexStore(Path.Combine(SamplePack.Root, "data", "book-index")).AllBooks();

    private static readonly LinkTargets Targets = LinkTargets.Build(Dossiers, Stats, Books);

    private static readonly AuthoredProjection Projection = new(Dossiers, Stats, Targets, Books);

    [Fact]
    public void Searching_part_of_an_areas_name_finds_that_area_first()
    {
        SearchMatch first = Projection.SearchNodes("bell-founder", 20)[0];

        Assert.Equal(Workshop, first.Id);
    }

    [Fact]
    public void An_area_link_resolves_on_the_floor_its_prose_belongs_to_and_on_no_other()
    {
        var resolver = new MarkupCrossRefResolver(Targets);
        DossierBlock overview = Dossiers.GetArea(LowerNave).Value.Glance.First(block => block.Heading == "Overview");

        Assert.Equal(
            NaveAndVestry,
            resolver.Resolve(overview.Body, Level1).Where(reference => reference.Kind == CrossRefKind.Area).Select(reference => reference.TargetId));

        // The same paragraph read on no floor, as the quest log and the decks read theirs, links no
        // area: "area 3a" names a room on every floor that has one.
        Assert.DoesNotContain(resolver.Resolve(overview.Body), reference => reference.Kind == CrossRefKind.Area);
    }

    [Fact]
    public void Search_reaches_the_book_and_a_book_entry_leads_to_the_campaigns_own_card()
    {
        SearchMatch hit = Projection.SearchNodes("Old Mill", 5)[0];
        Assert.Equal("the-old-mill", hit.Id);
        Assert.Equal("book", hit.Category);
        Assert.Equal("ALMANAC, p. 5", hit.Citation);

        Result<ReferenceCard> mill = Projection.GetReferenceCard(hit.Id);
        Assert.True(mill.IsValid);
        RelationLine miller = Assert.Single(mill.Value.Relations, line => line.OtherId == "wenna-brask");
        Assert.Equal("run by", miller.Phrase);

        // The book's Wenna is the campaign's NPC: the click opens her card, which the book extends.
        Assert.Equal(CrossRefKind.Npc, Targets.Resolve(miller.OtherId, string.Empty).Value.Kind);
        Assert.True(Projection.GetBookCard("wenna-brask").IsValid);
        Assert.Contains(Projection.RelationsOf("wenna-brask"), line => line.OtherId == "the-old-mill");
    }

    [Fact]
    public void A_stat_block_cites_its_book_by_the_title_the_book_index_gives()
    {
        StatBlock bugbear = Stats.AllMonsters().First(block => block.Name == "Bugbear");

        Assert.Equal("SRD 5.1 " + bugbear.SourceLocation, Projection.GetReferenceCard(bugbear.NodeId).Value.Citation);
    }
}
