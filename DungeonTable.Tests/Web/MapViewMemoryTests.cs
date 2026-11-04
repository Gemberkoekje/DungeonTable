using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// What the DM's map does with its pan and zoom as the dock goes between the reading tabs and the
/// fight: each layout keeps its own view, and the fight's room is framed the first time the DM goes to
/// that fight, after which the battle view is the DM's own.
/// </summary>
public sealed class MapViewMemoryTests
{
    private const string GoblinDen = "data_dossiers_level_1_area_2";
    private const string Workshop = "data_dossiers_level_1_area_4";

    [Theory]
    [InlineData(DockTab.Room, DockLayout.Reading)]
    [InlineData(DockTab.Campaign, DockLayout.Reading)]
    [InlineData(DockTab.Art, DockLayout.Reading)]
    [InlineData(DockTab.Battle, DockLayout.Battle)]
    public void The_battle_tab_has_a_layout_of_its_own_and_the_reading_tabs_share_one(DockTab tab, DockLayout layout)
    {
        Assert.Equal(layout, MapViewMemory.LayoutOf(tab));
    }

    [Fact]
    public void Going_to_a_fight_keeps_the_reading_view_and_frames_the_fights_room()
    {
        var views = new MapViewMemory();

        MapViewChange change = views.Change(DockTab.Room, DockTab.Battle, GoblinDen, fightAreaOnMap: true);

        Assert.Equal(new MapViewChange(DockLayout.Reading, DockLayout.None, true), change);
    }

    [Fact]
    public void Coming_back_from_the_fight_brings_back_the_reading_view()
    {
        var views = new MapViewMemory();
        views.Change(DockTab.Room, DockTab.Battle, GoblinDen, fightAreaOnMap: true);

        MapViewChange change = views.Change(DockTab.Battle, DockTab.Room, GoblinDen, fightAreaOnMap: true);

        Assert.Equal(new MapViewChange(DockLayout.Battle, DockLayout.Reading, false), change);
    }

    [Fact]
    public void The_same_fight_is_not_framed_twice_so_the_dms_own_battle_view_comes_back()
    {
        var views = new MapViewMemory();
        views.Change(DockTab.Room, DockTab.Battle, GoblinDen, fightAreaOnMap: true);
        views.Change(DockTab.Battle, DockTab.Campaign, GoblinDen, fightAreaOnMap: true);

        MapViewChange change = views.Change(DockTab.Campaign, DockTab.Battle, GoblinDen, fightAreaOnMap: true);

        Assert.Equal(new MapViewChange(DockLayout.Reading, DockLayout.Battle, false), change);
    }

    [Fact]
    public void A_fight_in_another_room_is_framed_afresh()
    {
        var views = new MapViewMemory();
        views.Change(DockTab.Room, DockTab.Battle, GoblinDen, fightAreaOnMap: true);
        views.Change(DockTab.Battle, DockTab.Room, GoblinDen, fightAreaOnMap: true);

        MapViewChange change = views.Change(DockTab.Room, DockTab.Battle, Workshop, fightAreaOnMap: true);

        Assert.True(change.FrameFight);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(GoblinDen, false)]
    public void With_no_fight_or_no_room_to_frame_the_battle_view_simply_comes_back(string fightArea, bool onMap)
    {
        var views = new MapViewMemory();

        MapViewChange change = views.Change(DockTab.Room, DockTab.Battle, fightArea, onMap);

        Assert.Equal(new MapViewChange(DockLayout.Reading, DockLayout.Battle, false), change);
    }

    [Fact]
    public void A_room_that_could_not_be_framed_is_framed_once_it_can_be()
    {
        // The fight started on another floor's map; the DM switches to its map and goes back to the fight.
        var views = new MapViewMemory();
        views.Change(DockTab.Room, DockTab.Battle, GoblinDen, fightAreaOnMap: false);
        views.Change(DockTab.Battle, DockTab.Room, GoblinDen, fightAreaOnMap: false);

        Assert.True(views.Change(DockTab.Room, DockTab.Battle, GoblinDen, fightAreaOnMap: true).FrameFight);
    }

    [Theory]
    [InlineData(DockTab.Room, DockTab.Campaign)]
    [InlineData(DockTab.Campaign, DockTab.Art)]
    [InlineData(DockTab.Battle, DockTab.Battle)]
    public void Moving_within_a_layout_leaves_the_map_alone(DockTab from, DockTab to)
    {
        var views = new MapViewMemory();

        Assert.Equal(MapViewChange.None, views.Change(from, to, GoblinDen, fightAreaOnMap: true));
    }
}
