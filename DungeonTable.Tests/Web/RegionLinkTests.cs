using System.Collections.Generic;
using DungeonTable.Core.Dossier;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// A map region opens its area when it is clicked, so the Room Editor offers a region areas only: the
/// areas the search finds, and an id typed in full, unless something other than an area is written
/// under it.
/// </summary>
public sealed class RegionLinkTests
{
    private static readonly SearchMatch[] Nothing = System.Array.Empty<SearchMatch>();

    // The two areas among the mixed results below, in the order the search ranked them (CA1861).
    private static readonly string[] TheTwoAreas = { "data_dossiers_level_1_area_4", "data_dossiers_level_1_area_3a" };

    [Fact]
    public void Only_the_areas_among_the_search_results_are_offered()
    {
        var found = new[]
        {
            new SearchMatch { Id = "grask", Label = "Grask", Category = "npc" },
            new SearchMatch { Id = "data_dossiers_level_1_area_4", Label = "Bell-Founder's Workshop (Level 1, Area 4)", Category = "area" },
            new SearchMatch { Id = "data_statblocks_monsters_bugbear", Label = "Bugbear", Category = "monster" },
            new SearchMatch { Id = "the-old-mill", Label = "The Old Mill", Category = "book" },
            new SearchMatch { Id = "data_dossiers_level_1_area_3a", Label = "Nave (Level 1, Area 3a)", Category = "area", Citation = "The Font" },
        };

        IReadOnlyList<SearchMatch> offered = RegionLink.Areas(found, 12);

        Assert.Equal(TheTwoAreas, offered.Select(match => match.Id));
        Assert.Single(RegionLink.Areas(found, 1));
    }

    [Fact]
    public void An_id_nothing_is_written_under_is_offered_as_an_area_to_come()
    {
        Assert.Equal("data_dossiers_level_2_area_1", RegionLink.Offer("data_dossiers_level_2_area_1", Nothing, _ => CrossRefKind.None));
        Assert.Equal(string.Empty, RegionLink.Refusal("data_dossiers_level_2_area_1", Nothing, _ => CrossRefKind.None));
    }

    [Fact]
    public void A_written_area_is_offered()
    {
        Assert.Equal("data_dossiers_level_1_area_5", RegionLink.Offer("data_dossiers_level_1_area_5", Nothing, _ => CrossRefKind.Area));
    }

    [Theory]
    [InlineData(CrossRefKind.Npc, "an NPC")]
    [InlineData(CrossRefKind.Monster, "a creature")]
    [InlineData(CrossRefKind.Book, "a book entry")]
    public void An_id_something_else_is_written_under_is_refused_and_says_why(CrossRefKind kind, string what)
    {
        Assert.Equal(string.Empty, RegionLink.Offer("grask", Nothing, _ => kind));
        Assert.Equal(
            $"'grask' is {what}, not an area. A region opens an area when it is clicked; link a marker to it instead.",
            RegionLink.Refusal("grask", Nothing, _ => kind));
    }

    [Fact]
    public void A_person_is_found_by_their_id_although_they_have_no_card_of_the_projections_own()
    {
        var content = new FakeContentProjection();
        content.Matches.Add(new SearchMatch { Id = "wenna-brask", Label = "Wenna Brask", Category = "npc" });
        content.Cards["data_dossiers_level_1_area_1"] = new ReferenceCard { NodeId = "data_dossiers_level_1_area_1", Kind = CrossRefKind.Area };

        Assert.Equal(CrossRefKind.Npc, RegionLink.WrittenAs(content, "wenna-brask"));
        Assert.Equal(CrossRefKind.Area, RegionLink.WrittenAs(content, "data_dossiers_level_1_area_1"));
        Assert.Equal(CrossRefKind.None, RegionLink.WrittenAs(content, "data_dossiers_level_2_area_1"));
    }
}
