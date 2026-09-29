using DungeonTable.Core.Dossier;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The Room Editor's link box finds what is written, and also takes an area id typed in full, so a
/// map can be annotated before its level file exists.
/// </summary>
public sealed class TypedNodeIdTests
{
    private static readonly SearchMatch[] Nothing = System.Array.Empty<SearchMatch>();

    [Fact]
    public void An_id_the_search_cannot_find_is_offered_as_typed()
    {
        Assert.Equal(
            "data_dossiers_level_2_area_1",
            TypedNodeId.Offer("  data_dossiers_level_2_area_1 ", Nothing));
    }

    [Fact]
    public void An_id_the_search_found_is_linked_from_the_results_instead()
    {
        var found = new[]
        {
            new SearchMatch { Id = "data_dossiers_level_1_area_3", Label = "The Lower Nave (Level 1, Area 3)" },
            new SearchMatch { Id = "data_dossiers_level_1_area_3a", Label = "Nave (Level 1, Area 3a)" },
        };

        Assert.Equal(string.Empty, TypedNodeId.Offer("data_dossiers_level_1_area_3a", found));
    }

    [Fact]
    public void A_near_miss_in_the_results_does_not_stop_the_typed_id_being_offered()
    {
        // A new sub-area on a written floor: the search finds its siblings by id, but not it.
        var siblings = new[]
        {
            new SearchMatch { Id = "data_dossiers_level_1_area_3a" },
            new SearchMatch { Id = "data_dossiers_level_1_area_3b" },
        };

        Assert.Equal("data_dossiers_level_1_area_3c", TypedNodeId.Offer("data_dossiers_level_1_area_3c", siblings));
    }

    [Fact]
    public void Ids_are_compared_exactly()
    {
        var found = new[] { new SearchMatch { Id = "bell-warden" } };

        Assert.Equal("Bell-Warden", TypedNodeId.Offer("Bell-Warden", found));
    }

    [Fact]
    public void A_name_with_spaces_is_a_search_not_an_id()
    {
        Assert.Equal(string.Empty, TypedNodeId.Offer("goblin den", Nothing));
        Assert.Equal(string.Empty, TypedNodeId.Offer("   ", Nothing));
        Assert.Equal(string.Empty, TypedNodeId.Offer(string.Empty, Nothing));
    }
}
