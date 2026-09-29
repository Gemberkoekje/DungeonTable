using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Content;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Verifies the per-circuit DM workspace the Map / Info / Battle tabs share: loading a map and its
/// annotations, navigating the briefing, resolving which regions belong to the shown room, and the
/// derived readouts the shell's status bar and the Info tab's mini map use.
/// </summary>
public sealed class DmWorkspaceTests
{
    private const string Landing = "data_dossiers_level_1_area_1";
    private const string Pillars = "data_dossiers_level_1_area_2";
    private const string Level1 = "data_dossiers_level_1";

    private static readonly string[] LandingRegionIds = { "r-entry-a", "r-entry-b" };

    private static readonly string[] PillarSearchIds = { Pillars, "data_dossiers_level_1_area_2a" };

    private static readonly string[] FloorTwoThenNone = { "data_dossiers_level_2", string.Empty };

    private static readonly string[] OmensThenRumours = { "omens", "village-rumours" };

    private static readonly NpcDossier Wenna = new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask", Summary = "The miller." };

    private static readonly CharacterDossier Maren = new CharacterDossier { Id = "maren", Name = "Maren" };

    [Fact]
    public async Task Initialising_loads_the_first_map_its_annotations_and_the_default_area()
    {
        Harness harness = Harness.Build();

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.True(harness.Workspace.MapLoaded);
        Assert.Equal("level-1", harness.Workspace.CurrentMapId);
        Assert.Equal(string.Empty, harness.Workspace.MapError);
        Assert.Equal(4, harness.Workspace.Regions.Count);
        Assert.Equal(Landing, harness.Workspace.PanelNode);
        Assert.Equal("level-1", harness.Session.CurrentMapId);
    }

    [Fact]
    public async Task The_map_picker_lists_each_map_under_the_level_name_the_editor_gave_it()
    {
        Harness harness = Harness.Build();
        MapDefinition level1 = harness.Annotations.Definitions["level-1"];
        harness.Annotations.Definitions["level-1"] = new MapDefinition
        {
            MapId = level1.MapId,
            LevelName = "Level 1 - The Undercroft",
            Regions = level1.Regions,
        };

        // A second map, drawn but not yet annotated, has no name to show but its id.
        harness.Maps.Maps["level-2-crypt"] = new VectorMap { MapId = "level-2-crypt", Bounds = new MapBounds(0, 0, 100, 100) };

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Equal("Level 1 - The Undercroft", harness.Workspace.MapLabel("level-1"));
        Assert.Equal("level-2-crypt", harness.Workspace.MapLabel("level-2-crypt"));
    }

    [Fact]
    public async Task The_screen_opens_on_the_area_the_campaign_names()
    {
        Harness harness = Harness.Build();
        harness.Dossiers.Campaign = new CampaignDossier { StartAreaNodeId = "  " + Pillars + " " };

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Equal(Pillars, harness.Workspace.PanelNode);
        Assert.Equal(Pillars, Assert.Single(harness.Workspace.History.Entries).NodeId);
    }

    [Fact]
    public async Task With_no_start_area_named_the_screen_opens_on_the_first_authored_area()
    {
        // "First" is the store's load order (the first area of the first level file), not whichever
        // id sorts first; an area with no id cannot be opened, so it is passed over.
        Harness harness = Harness.Build();
        harness.Dossiers.Campaign = new CampaignDossier { StartAreaNodeId = "   " };
        harness.Dossiers.Areas[string.Empty] = new AreaDossier { Title = "An area with no id" };
        harness.Dossiers.Areas[Pillars] = new AreaDossier { AreaNodeId = Pillars, Title = "Pillars" };
        harness.Dossiers.Areas[Landing] = new AreaDossier { AreaNodeId = Landing, Title = "Undercroft Landing" };

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Equal(Pillars, harness.Workspace.PanelNode);
    }

    [Fact]
    public async Task A_campaign_with_no_areas_opens_on_an_empty_panel()
    {
        Harness harness = Harness.Build();
        harness.Dossiers.HasCampaign = false;

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Equal(string.Empty, harness.Workspace.PanelNode);
        Assert.Empty(harness.Workspace.History.Entries);
        Assert.True(harness.Workspace.MapLoaded);
    }

    [Fact]
    public async Task A_named_start_area_that_is_not_authored_opens_on_its_own_message()
    {
        // Falling back to some other room would hide the typo; the panel's "not found" names it.
        Harness harness = Harness.Build();
        harness.Dossiers.Campaign = new CampaignDossier { StartAreaNodeId = "data_dossiers_level_9_area_1" };
        harness.Dossiers.Areas[Pillars] = new AreaDossier { AreaNodeId = Pillars, Title = "Pillars" };

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Equal("data_dossiers_level_9_area_1", harness.Workspace.PanelNode);
        Assert.Contains("data_dossiers_level_9_area_1", harness.Workspace.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Initialising_twice_is_a_no_op_so_returning_to_the_page_keeps_its_state()
    {
        Harness harness = Harness.Build();
        await harness.Workspace.InitialiseAsync(CancellationToken.None);
        harness.Workspace.NavigateTo(Landing);

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Equal(Landing, harness.Workspace.PanelNode);
    }

    [Fact]
    public async Task Loading_an_unknown_map_reports_the_error_instead_of_a_stale_map()
    {
        Harness harness = Harness.Build();
        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        await harness.Workspace.LoadMapAsync("no-such-map", CancellationToken.None);

        Assert.False(harness.Workspace.MapLoaded);
        Assert.Contains("no-such-map", harness.Workspace.MapError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Door_annotations_are_indexed_by_the_state_the_renderer_needs()
    {
        Harness harness = Harness.Build();

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Contains("door-secret", harness.Workspace.SecretDoorIds);
        Assert.Contains("door-double", harness.Workspace.DoubleDoorIds);
        Assert.Contains("statue", harness.Workspace.ConcealedObjectIds);

        // A concealed DOOR is not a concealed object: it hides as a secret door instead.
        Assert.DoesNotContain("door-secret", harness.Workspace.ConcealedObjectIds);

        // The map's default-open doors are opened for the players when the map is first shown.
        Assert.Contains("door-open", harness.Session.OpenDoorIds());
    }

    [Fact]
    public async Task An_unreadable_annotation_document_warns_rather_than_failing_the_map()
    {
        Harness harness = Harness.Build();
        harness.Annotations.Definitions.Remove("level-1");

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        // The document is simply absent here, which is a first-time map, not a broken one.
        Assert.True(harness.Workspace.MapLoaded);
        Assert.Equal(string.Empty, harness.Workspace.AnnotationsWarning);
        Assert.Empty(harness.Workspace.Regions);
    }

    [Fact]
    public async Task Navigating_records_history_and_resolves_the_rooms_regions()
    {
        Harness harness = await Harness.Started();

        harness.Workspace.NavigateTo(Landing);

        Assert.Equal(Landing, harness.Workspace.PanelNode);
        Assert.Equal(LandingRegionIds, harness.Workspace.RoomRegionIds.ToArray());
        Assert.Equal("r-entry-a", harness.Workspace.SelectedRegionId);
        Assert.Equal(Landing, harness.Workspace.History.Current.NodeId);
    }

    [Fact]
    public async Task Opening_an_entity_reference_keeps_the_panel_on_the_room_it_came_from()
    {
        Harness harness = await Harness.Started();
        harness.Workspace.NavigateTo(Landing);

        harness.Workspace.OpenReference("data_statblocks_monsters_bugbear");

        Assert.Equal("data_statblocks_monsters_bugbear", harness.Workspace.CardNode);
        Assert.Equal("Bugbear", harness.Workspace.ReferenceCard.Label);
        Assert.Equal(Landing, harness.Workspace.PanelNode);

        // Closing the drawer returns to that room without losing the card from the trail.
        harness.Workspace.CloseDrawer();
        Assert.Equal(string.Empty, harness.Workspace.CardNode);
        Assert.True(harness.Workspace.History.CanForward);
    }

    [Fact]
    public async Task Opening_a_stat_block_shows_the_extracted_block()
    {
        Harness harness = await Harness.Started();

        harness.Workspace.OpenReference("data_statblocks_monsters_bandit");

        Assert.Equal("data_statblocks_monsters_bandit", harness.Workspace.OpenStatBlock.NodeId);
    }

    [Fact]
    public async Task An_open_stat_block_is_cited_as_its_card_cites_it_and_the_next_card_forgets_it()
    {
        // The full block cited its raw source file ("SRD_CC_v5.1.pdf p.35") where its card and the
        // encounter strip said "SRD 5.1 p.35".
        Harness harness = await Harness.Started();
        harness.Content.Cards["data_statblocks_monsters_bandit"] = new ReferenceCard
        {
            NodeId = "data_statblocks_monsters_bandit",
            Label = "Bandit",
            Kind = CrossRefKind.Monster,
            Citation = "SRD 5.1 p.35",
        };

        harness.Workspace.OpenReference("data_statblocks_monsters_bandit");
        Assert.Equal("SRD 5.1 p.35", harness.Workspace.OpenStatBlockCitation);

        harness.Workspace.OpenReference("data_statblocks_monsters_bugbear");
        Assert.Equal(string.Empty, harness.Workspace.OpenStatBlockCitation);
    }

    [Fact]
    public async Task A_content_change_reads_the_campaign_and_the_open_card_again()
    {
        // Live reload: the file behind the NPC tab changed on disk while the DM had her card open.
        Harness harness = Harness.Build();
        harness.Dossiers.Npcs = new NpcRoster { Npcs = new[] { Wenna } };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);
        harness.Workspace.OpenReference("wenna-brask");
        int stops = harness.Workspace.History.Entries.Count;

        harness.Dossiers.Npcs = new NpcRoster { Npcs = new[] { new NpcDossier { Id = "wenna-brask", Name = "Wenna Mill" } } };
        await harness.Workspace.RefreshAsync(ContentChanges.Documents, CancellationToken.None);

        Assert.Equal("Wenna Mill", Assert.Single(harness.Workspace.NpcRoster.Npcs).Name);
        Assert.Equal("Wenna Mill", harness.Workspace.OpenNpc.Name);
        Assert.Equal("wenna-brask", harness.Workspace.CardNode);
        Assert.Equal(stops, harness.Workspace.History.Entries.Count);
    }

    [Fact]
    public async Task A_map_change_reads_the_map_again_and_keeps_what_the_table_did()
    {
        // The Room Editor saved the map on show: its new region appears, and nothing the table revealed,
        // framed or opened is undone or seeded again.
        Harness harness = await Harness.Started();
        harness.Session.ToggleRegion("r-pillars");
        MapBounds framing = harness.Session.PlayerViewport;
        MapDefinition notes = harness.Annotations.Definitions["level-1"];
        harness.Annotations.Definitions["level-1"] = new MapDefinition
        {
            MapId = notes.MapId,
            Regions = notes.Regions.Append(new Region
            {
                RegionId = "r-cellar",
                Polygon = new[] { new MapPoint(500, 500), new MapPoint(540, 500), new MapPoint(540, 540) },
            }).ToArray(),
            Objects = notes.Objects,
        };

        await harness.Workspace.RefreshAsync(ContentChanges.Maps, CancellationToken.None);

        Assert.Contains(harness.Workspace.Regions, region => region.RegionId == "r-cellar");
        Assert.Contains("r-pillars", harness.Session.RevealedRegionIds());
        Assert.Equal(framing, harness.Session.PlayerViewport);
        Assert.Equal("level-1", harness.Workspace.CurrentMapId);
    }

    [Fact]
    public async Task A_new_dm_window_opens_the_map_the_table_is_on()
    {
        // It opened the first map, and pointing the shared session at it cleared the other map's reveals.
        Harness harness = Harness.Build();
        harness.Maps.Maps["level-2"] = new VectorMap { MapId = "level-2", Bounds = new MapBounds(0, 0, 500, 400), Grid = new Grid { CellSizePx = 10 } };
        harness.Annotations.Definitions["level-2"] = new MapDefinition { MapId = "level-2" };
        harness.Session.SetCurrentMap("level-2");
        harness.Session.ToggleRegion("r-crypt");

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Equal("level-2", harness.Workspace.CurrentMapId);
        Assert.Contains("r-crypt", harness.Session.RevealedRegionIds());
    }

    [Fact]
    public async Task A_first_map_saved_while_the_page_is_open_opens_by_itself()
    {
        // A new campaign: the DM screen was open before the first map was saved into Maps/, and it
        // went on saying "No maps are available" until the page was loaded again.
        Harness harness = Harness.Build();
        VectorMap drawn = harness.Maps.Maps["level-1"];
        harness.Maps.Maps.Clear();
        await harness.Workspace.InitialiseAsync(CancellationToken.None);
        Assert.False(harness.Workspace.MapLoaded);

        harness.Maps.Maps["level-1"] = drawn;
        await harness.Workspace.RefreshAsync(ContentChanges.Maps, CancellationToken.None);

        Assert.True(harness.Workspace.MapLoaded);
        Assert.Equal("level-1", harness.Workspace.CurrentMapId);
        Assert.Equal("level-1", harness.Session.CurrentMapId);
    }

    [Fact]
    public async Task A_page_with_no_map_opens_the_one_another_dm_window_chose()
    {
        Harness harness = Harness.Build();
        VectorMap drawn = harness.Maps.Maps["level-1"];
        harness.Maps.Maps.Clear();
        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        harness.Maps.Maps["level-1"] = drawn;
        harness.Maps.Maps["level-2"] = new VectorMap { MapId = "level-2", Bounds = new MapBounds(0, 0, 500, 400), Grid = new Grid { CellSizePx = 10 } };
        harness.Annotations.Definitions["level-2"] = new MapDefinition { MapId = "level-2" };
        harness.Session.SetCurrentMap("level-2");
        await harness.Workspace.RefreshAsync(ContentChanges.Maps, CancellationToken.None);

        Assert.Equal("level-2", harness.Workspace.CurrentMapId);
    }

    [Fact]
    public async Task A_map_on_show_that_goes_missing_is_not_swapped_for_another()
    {
        // Pointing the table at another map clears the reveals of the one in play, so a map file renamed
        // or deleted mid-session must not do that behind the DM's back.
        Harness harness = await Harness.Started();
        harness.Session.ToggleRegion("r-pillars");
        harness.Maps.Maps["level-0"] = new VectorMap { MapId = "level-0", Bounds = new MapBounds(0, 0, 500, 400), Grid = new Grid { CellSizePx = 10 } };
        harness.Maps.Maps.Remove("level-1");

        await harness.Workspace.RefreshAsync(ContentChanges.Maps, CancellationToken.None);

        Assert.Equal("level-1", harness.Workspace.CurrentMapId);
        Assert.Contains("r-pillars", harness.Session.RevealedRegionIds());
    }

    [Fact]
    public async Task A_floor_is_written_when_a_level_file_carries_its_number()
    {
        // The quest log asks this of a beat on a floor, which it marked "not authored yet" on every one.
        Harness harness = await Harness.Started();
        harness.Dossiers.LevelNumbers[Level1] = 2;

        Assert.True(harness.Workspace.IsFloorAuthored(2));
        Assert.False(harness.Workspace.IsFloorAuthored(1));
        Assert.False(harness.Workspace.IsFloorAuthored(3));
        Assert.False(harness.Workspace.IsFloorAuthored(0));
    }

    [Fact]
    public async Task A_card_that_is_not_a_stat_block_shows_none_even_under_a_blocks_id()
    {
        // Only a monster card opens the block with its own id; a faction or a book entry never does.
        Harness harness = await Harness.Started();
        harness.Content.Cards["data_statblocks_monsters_bandit"] = new ReferenceCard { NodeId = "data_statblocks_monsters_bandit", Label = "Bandits", Kind = CrossRefKind.Faction };

        harness.Workspace.OpenReference("data_statblocks_monsters_bandit");

        Assert.Equal(string.Empty, harness.Workspace.OpenStatBlock.NodeId);
    }

    [Fact]
    public async Task A_reference_that_is_an_area_navigates_instead_of_opening_a_card()
    {
        Harness harness = await Harness.Started();

        harness.Workspace.OpenReference(Pillars);

        Assert.Equal(string.Empty, harness.Workspace.CardNode);
        Assert.Equal(Pillars, harness.Workspace.PanelNode);
    }

    [Fact]
    public async Task A_stat_block_or_spell_card_opens_over_the_room_with_its_entry()
    {
        Harness harness = Harness.Build();
        harness.Stats.Monsters["data_statblocks_monsters_ogre"] = new StatBlock { NodeId = "data_statblocks_monsters_ogre", Name = "Ogre" };
        harness.Stats.Spells["data_statblocks_spells_web"] = new SpellEntry { NodeId = "data_statblocks_spells_web", Name = "Web" };
        harness.Content.Cards["data_statblocks_monsters_ogre"] = new ReferenceCard { NodeId = "data_statblocks_monsters_ogre", Label = "Ogre", Kind = CrossRefKind.Monster };
        harness.Content.Cards["data_statblocks_spells_web"] = new ReferenceCard { NodeId = "data_statblocks_spells_web", Label = "Web", Kind = CrossRefKind.Spell };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);
        harness.Workspace.NavigateTo(Landing);

        harness.Workspace.OpenReference("data_statblocks_monsters_ogre");

        Assert.Equal("Ogre", harness.Workspace.ReferenceCard.Label);
        Assert.Equal("data_statblocks_monsters_ogre", harness.Workspace.OpenStatBlock.NodeId);
        Assert.Equal(Landing, harness.Workspace.PanelNode);
        Assert.Equal(string.Empty, harness.Workspace.ErrorMessage);

        harness.Workspace.OpenReference("data_statblocks_spells_web");

        Assert.Equal(CrossRefKind.Spell, harness.Workspace.ReferenceCard.Kind);
        Assert.Equal("data_statblocks_spells_web", harness.Workspace.OpenSpellEntry.NodeId);
    }

    [Fact]
    public async Task An_id_nothing_has_a_card_for_goes_to_the_panel_and_says_so()
    {
        Harness harness = await Harness.Started();

        harness.Workspace.OpenReference("data_statblocks_monsters_nothing");

        Assert.Equal(string.Empty, harness.Workspace.CardNode);
        Assert.Equal("data_statblocks_monsters_nothing", harness.Workspace.PanelNode);
        Assert.NotEqual(string.Empty, harness.Workspace.ErrorMessage);
    }

    [Fact]
    public async Task An_npcs_roster_id_opens_their_card_over_the_room_instead_of_navigating()
    {
        // The projection builds no card for a roster id, which used to send it to the panel as a room.
        Harness harness = Harness.Build();
        harness.Dossiers.Npcs = new NpcRoster { Npcs = new[] { Wenna } };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);
        harness.Workspace.NavigateTo(Landing);

        harness.Workspace.OpenReference("wenna-brask");

        Assert.Equal("wenna-brask", harness.Workspace.CardNode);
        Assert.Same(Wenna, harness.Workspace.OpenNpc);
        Assert.Equal(CrossRefKind.Npc, harness.Workspace.ReferenceCard.Kind);
        Assert.Equal("Wenna Brask", harness.Workspace.ReferenceCard.Label);
        Assert.Equal(Landing, harness.Workspace.PanelNode);
        Assert.Equal(string.Empty, harness.Workspace.ErrorMessage);
        Assert.Equal(new NavEntry("wenna-brask", CrossRefKind.Npc, "Wenna Brask"), harness.Workspace.History.Current);
    }

    [Fact]
    public async Task An_npc_who_fights_shows_the_stat_block_they_use_beneath_their_card()
    {
        const string Acolyte = "data_statblocks_monsters_acolyte";
        var aldous = new NpcDossier { Id = "brother-aldous", Name = "Brother Aldous", StatBlock = "acolyte" };
        Harness harness = Harness.Build();
        harness.Dossiers.Npcs = new NpcRoster { Npcs = new[] { aldous, Wenna } };
        harness.Content.StatBlocks["brother-aldous"] = Acolyte;
        harness.Stats.Monsters[Acolyte] = new StatBlock { NodeId = Acolyte, Name = "Acolyte" };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        harness.Workspace.OpenReference("brother-aldous");

        Assert.Same(aldous, harness.Workspace.OpenNpc);
        Assert.Equal(Acolyte, harness.Workspace.ReferenceCard.StatBlockId);
        Assert.Equal("Acolyte", harness.Workspace.OpenStatBlock.Name);

        // An NPC who uses no block shows none, rather than the last card's.
        harness.Workspace.OpenReference("wenna-brask");

        Assert.Equal(string.Empty, harness.Workspace.ReferenceCard.StatBlockId);
        Assert.Equal(string.Empty, harness.Workspace.OpenStatBlock.NodeId);
    }

    [Fact]
    public async Task What_the_book_says_extends_the_campaigns_own_card_and_goes_when_the_card_does()
    {
        Harness harness = Harness.Build();
        harness.Dossiers.Npcs = new NpcRoster { Npcs = new[] { Wenna } };
        harness.Content.BookCards["wenna-brask"] = new ReferenceCard
        {
            NodeId = "wenna-brask",
            Kind = CrossRefKind.Book,
            Summary = "A dwarf smith, in the book's words.",
            Citation = "A Millbrook Almanac, p. 6",
        };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        harness.Workspace.OpenReference("wenna-brask");
        Assert.Equal("A dwarf smith, in the book's words.", harness.Workspace.BookCard.Summary);

        harness.Workspace.CloseDrawer();
        Assert.Equal(string.Empty, harness.Workspace.BookCard.NodeId);
    }

    [Fact]
    public async Task A_book_entrys_own_card_is_not_extended_by_itself()
    {
        Harness harness = Harness.Build();
        var mill = new ReferenceCard { NodeId = "the-old-mill", Label = "The Old Mill", Kind = CrossRefKind.Book, Summary = "A watermill." };
        harness.Content.Cards["the-old-mill"] = mill;
        harness.Content.BookCards["the-old-mill"] = mill;
        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        harness.Workspace.OpenReference("the-old-mill");

        Assert.Equal(CrossRefKind.Book, harness.Workspace.ReferenceCard.Kind);
        Assert.Equal(string.Empty, harness.Workspace.BookCard.NodeId);
    }

    [Fact]
    public async Task A_player_characters_id_opens_their_card()
    {
        Harness harness = Harness.Build();
        harness.Dossiers.Party = new PartyDossier { Characters = new[] { Maren } };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        harness.Workspace.OpenReference("maren");

        Assert.Same(Maren, harness.Workspace.OpenCharacter);
        Assert.Equal(CrossRefKind.Character, harness.Workspace.ReferenceCard.Kind);
        Assert.Equal(new NavEntry("maren", CrossRefKind.Character, "Maren"), harness.Workspace.History.Current);
    }

    [Fact]
    public async Task Moving_on_from_a_persons_card_leaves_nothing_of_them_behind()
    {
        Harness harness = Harness.Build();
        harness.Dossiers.Npcs = new NpcRoster { Npcs = new[] { Wenna } };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);
        harness.Workspace.OpenReference("wenna-brask");

        harness.Workspace.OpenReference("data_statblocks_monsters_bandit");

        Assert.Equal(string.Empty, harness.Workspace.OpenNpc.Id);
        Assert.Equal("data_statblocks_monsters_bandit", harness.Workspace.OpenStatBlock.NodeId);

        // Back returns to the person; closing the drawer clears them.
        harness.Workspace.NavigateBack();
        Assert.Same(Wenna, harness.Workspace.OpenNpc);
        Assert.Equal(string.Empty, harness.Workspace.OpenStatBlock.NodeId);

        harness.Workspace.CloseDrawer();
        Assert.Equal(string.Empty, harness.Workspace.OpenNpc.Id);
    }

    [Fact]
    public async Task Clicking_a_linked_region_navigates_to_its_briefing()
    {
        Harness harness = await Harness.Started();

        harness.Workspace.SelectRegion("r-pillars");

        Assert.Equal(Pillars, harness.Workspace.PanelNode);
        Assert.Equal(string.Empty, harness.Workspace.RegionNotice);
    }

    [Theory]
    [InlineData("data_statblocks_monsters_bugbear")]
    [InlineData("wenna-brask")]
    public async Task Clicking_a_region_linked_to_something_other_than_an_area_opens_its_card(string linked)
    {
        // The Room Editor used to offer a person or a stat block to link a region to, and a click on
        // such a region navigated the panel to an "area" that does not exist.
        Harness harness = Harness.Build();
        harness.Dossiers.Npcs = new NpcRoster { Npcs = new[] { Wenna } };
        MapDefinition notes = harness.Annotations.Definitions["level-1"];
        harness.Annotations.Definitions["level-1"] = new MapDefinition
        {
            MapId = notes.MapId,
            Regions = notes.Regions.Append(new Region
            {
                RegionId = "r-linked-wrongly",
                GraphNodeId = linked,
                Polygon = new[] { new MapPoint(500, 500), new MapPoint(540, 500), new MapPoint(540, 540) },
            }).ToArray(),
        };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);
        harness.Workspace.NavigateTo(Landing);

        harness.Workspace.SelectRegion("r-linked-wrongly");

        Assert.Equal(linked, harness.Workspace.CardNode);
        Assert.Equal(Landing, harness.Workspace.PanelNode);
        Assert.Equal(string.Empty, harness.Workspace.ErrorMessage);
    }

    [Fact]
    public async Task Clicking_a_region_linked_to_an_unwritten_area_still_navigates_to_it()
    {
        // An area id typed ahead of its floor is still an area: the panel says it is not written yet.
        const string Unwritten = "data_dossiers_level_2_area_1";
        Harness harness = Harness.Build();
        MapDefinition notes = harness.Annotations.Definitions["level-1"];
        harness.Annotations.Definitions["level-1"] = new MapDefinition
        {
            MapId = notes.MapId,
            Regions = notes.Regions.Append(new Region
            {
                RegionId = "r-ahead",
                GraphNodeId = Unwritten,
                Polygon = new[] { new MapPoint(500, 500), new MapPoint(540, 500), new MapPoint(540, 540) },
            }).ToArray(),
        };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        harness.Workspace.SelectRegion("r-ahead");

        Assert.Equal(Unwritten, harness.Workspace.PanelNode);
        Assert.Equal(string.Empty, harness.Workspace.CardNode);
    }

    [Fact]
    public async Task Clicking_an_unlinked_region_explains_itself_and_leaves_the_panel_alone()
    {
        Harness harness = await Harness.Started();
        harness.Workspace.NavigateTo(Landing);
        int before = harness.Workspace.History.Entries.Count;

        harness.Workspace.SelectRegion("r-unlinked");

        Assert.Equal(Landing, harness.Workspace.PanelNode);
        Assert.Equal("r-unlinked", harness.Workspace.SelectedRegionId);
        Assert.Contains("no linked briefing", harness.Workspace.RegionNotice, StringComparison.Ordinal);
        Assert.Equal(before, harness.Workspace.History.Entries.Count);
    }

    [Fact]
    public async Task Revealing_the_room_flips_every_region_that_belongs_to_it()
    {
        Harness harness = await Harness.Started();
        harness.Workspace.NavigateTo(Landing);

        Assert.False(harness.Workspace.RoomRevealed());
        harness.Workspace.ToggleRoomReveal();

        Assert.True(harness.Workspace.RoomRevealed());
        Assert.Equal(2, harness.Session.RevealedRegionIds().Count);

        // A partly-revealed room reveals the rest rather than hiding what is already shown.
        harness.Session.ToggleRegion("r-entry-b");
        Assert.False(harness.Workspace.RoomRevealed());
        harness.Workspace.ToggleRoomReveal();
        Assert.True(harness.Workspace.RoomRevealed());
    }

    [Fact]
    public async Task The_selected_bounds_frame_the_whole_room_across_its_regions()
    {
        Harness harness = await Harness.Started();
        harness.Workspace.NavigateTo(Landing);

        Assert.Equal(2, harness.Workspace.SelectedRegions().Count);
        Assert.Equal(new MapBounds(0, 0, 60, 40), harness.Workspace.SelectedBounds());
    }

    [Fact]
    public async Task The_viewport_readout_names_the_region_under_its_centre()
    {
        Harness harness = await Harness.Started();

        harness.Session.SetPlayerViewport(new MapBounds(0, 0, 40, 40));
        Assert.Equal("Undercroft Landing", harness.Workspace.ViewportRegionName());

        // Over open floor with no region, there is nothing to name.
        harness.Session.SetPlayerViewport(new MapBounds(600, 600, 700, 700));
        Assert.Equal(string.Empty, harness.Workspace.ViewportRegionName());
    }

    [Fact]
    public async Task The_search_tab_is_the_projections_search()
    {
        Harness harness = await Harness.Started();
        harness.Content.Matches.Add(new SearchMatch { Id = Pillars, Label = "Pillars", Category = "area" });
        harness.Content.Matches.Add(new SearchMatch
        {
            Id = "data_dossiers_level_1_area_2a",
            Label = "Demon Reliefs (Level 1, Area 2a)",
            Category = "area",
        });

        IReadOnlyList<SearchMatch> matches = harness.Workspace.SearchNodes("pillar");

        Assert.Equal(PillarSearchIds, matches.Select(match => match.Id));
    }

    [Fact]
    public async Task The_quest_log_is_loaded_once_beside_the_rules_reference()
    {
        Harness harness = Harness.Build();
        harness.Dossiers.Quests = new QuestLog
        {
            Quests = new[] { new Quest { Id = "the-lost-hymnal", Title = "The Lost Hymnal" } },
        };

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Equal("the-lost-hymnal", Assert.Single(harness.Workspace.QuestLog.Quests).Id);
    }

    [Fact]
    public async Task A_quest_beat_target_is_a_live_jump_only_once_its_area_is_authored()
    {
        // Every front-matter destination is on an unauthored level, so the chip stays a muted label
        // until that level lands — and lights up with no re-authoring when it does.
        Harness harness = await Harness.Started();
        harness.Dossiers.Areas[Landing] = new AreaDossier { AreaNodeId = Landing, Title = "Undercroft Landing" };

        Assert.True(harness.Workspace.IsAreaAuthored(Landing));
        Assert.False(harness.Workspace.IsAreaAuthored("data_dossiers_level_2_area_1"));
        Assert.False(harness.Workspace.IsAreaAuthored(string.Empty));
    }

    [Fact]
    public async Task The_campaign_dossier_and_the_decks_are_loaded_too()
    {
        Harness harness = Harness.Build();
        harness.Dossiers.Campaign = new CampaignDossier
        {
            Title = "The Silent Bell",
            Levels = new[] { new LevelSummary { Level = 1, Name = "The Undercroft", Authored = true } },
        };
        harness.Dossiers.Decks.Add(new CardDeck
        {
            Id = "omens",
            Title = "Omens of the Bell",
            Cards = new[] { new DeckCard { Id = "cracked-bell", Name = "Cracked Bell" } },
        });
        harness.Dossiers.Decks.Add(new CardDeck { Id = "village-rumours", Title = "Village Rumours" });

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Equal("The Silent Bell", harness.Workspace.CampaignDossier.Title);
        Assert.Equal("The Undercroft", Assert.Single(harness.Workspace.CampaignDossier.Levels).Name);

        // Every deck the campaign authors, in its order: the workspace picks none out by name.
        Assert.Equal(OmensThenRumours, harness.Workspace.Decks.Select(deck => deck.Id).ToArray());
        Assert.Equal("cracked-bell", Assert.Single(harness.Workspace.Decks[0].Cards).Id);
    }

    [Fact]
    public async Task An_unauthored_campaign_or_deck_leaves_an_empty_one_rather_than_throwing()
    {
        // The store reports "not authored" as an invalid Result, and reading .Value off one throws.
        // The fake serves no campaign and no decks by default, which is the shape a table with no
        // campaign.json has.
        Harness harness = Harness.Build();
        harness.Dossiers.HasCampaign = false;

        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        Assert.Empty(harness.Workspace.CampaignDossier.Sections);
        Assert.Empty(harness.Workspace.CampaignDossier.Levels);
        Assert.Empty(harness.Workspace.Decks);
    }

    [Fact]
    public void Quest_and_deck_prose_keep_every_link_the_resolver_finds()
    {
        // Read on no floor, a bare [[area 14c]] resolves to nothing; one that names its floor
        // ([[L1 area 14c]]) is a jump. Either way the span comes back: one left out would reach the
        // page as its raw markup.
        Harness harness = Harness.Build();
        harness.CrossRefs.Refs.Add(new CrossRef
        {
            Text = "area 14c",
            Start = 0,
            Length = 17,
            TargetId = "data_dossiers_level_1_area_14c",
            Kind = CrossRefKind.Area,
        });
        harness.CrossRefs.Refs.Add(new CrossRef
        {
            Text = "hobgoblin",
            Start = 21,
            Length = 13,
            TargetId = "data_statblocks_monsters_hobgoblin",
            Kind = CrossRefKind.Monster,
        });

        IReadOnlyList<CrossRef> refs = harness.Workspace.ResolveEntityRefs("[[L1 area 14c]], a [[hobgoblin]]");

        Assert.Equal(2, refs.Count);
        Assert.Equal(CrossRefKind.Area, refs[0].Kind);
    }

    [Fact]
    public async Task Room_prose_is_scanned_against_the_floor_the_shown_area_sits_on()
    {
        // The resolver keeps each floor's areas in its own bucket, so passing the wrong floor (or
        // none) silently costs every "area N" link on the panel — invisible in a rendered page
        // because a plain word and an unlinked word look identical.
        Harness harness = await Harness.Started();
        harness.Workspace.NavigateTo(Landing);

        harness.CrossRefs.ScopedTo.Clear();
        harness.Workspace.ResolveRefs("Beyond area 19 lies the crypt.");

        Assert.Equal(Level1, Assert.Single(harness.CrossRefs.ScopedTo));
    }

    [Fact]
    public async Task Dungeon_wide_prose_names_no_floor_at_all()
    {
        // A quest beat or a deck card is about the whole dungeon, so it must be scanned with no
        // floor even while the panel is standing in one.
        Harness harness = await Harness.Started();
        harness.Workspace.NavigateTo(Landing);

        harness.CrossRefs.ScopedTo.Clear();
        harness.Workspace.ResolveEntityRefs("Seek the founders on level 2, area 1.");

        Assert.Equal(string.Empty, Assert.Single(harness.CrossRefs.ScopedTo));
    }

    [Fact]
    public async Task An_area_on_no_authored_floor_falls_back_to_no_floor()
    {
        // A region linked to a node that has no dossier has no floor to be read against.
        // Guessing one would be the wrong-floor link the scoping exists to prevent.
        Harness harness = await Harness.Started();
        harness.Workspace.NavigateTo("data_dossiers_level_1_area_no_dossier");

        harness.CrossRefs.ScopedTo.Clear();
        harness.Workspace.ResolveRefs("Beyond area 19 lies the crypt.");

        Assert.Equal(string.Empty, Assert.Single(harness.CrossRefs.ScopedTo));
    }

    [Fact]
    public async Task An_individuals_card_opens_with_the_stat_block_it_uses()
    {
        // The card is the Bell-Warden's, the numbers are every gargoyle's.
        Harness harness = await Harness.Started();
        harness.Stats.Monsters["data_statblocks_monsters_gargoyle"] = new StatBlock { NodeId = "data_statblocks_monsters_gargoyle", Name = "Gargoyle" };
        harness.Content.Cards["bell-warden"] = new ReferenceCard
        {
            NodeId = "bell-warden",
            Label = "the Bell-Warden",
            Kind = CrossRefKind.Monster,
            Summary = "Guards the nave.",
            StatBlockId = "data_statblocks_monsters_gargoyle",
            LevelNodeId = Level1,
        };
        harness.Workspace.NavigateTo(Landing);

        harness.Workspace.OpenReference("bell-warden");

        Assert.Equal("bell-warden", harness.Workspace.CardNode);
        Assert.Equal("the Bell-Warden", harness.Workspace.ReferenceCard.Label);
        Assert.Equal("data_statblocks_monsters_gargoyle", harness.Workspace.OpenStatBlock.NodeId);
        Assert.Equal(Landing, harness.Workspace.PanelNode);
        Assert.Equal(new NavEntry("bell-warden", CrossRefKind.Monster, "the Bell-Warden"), harness.Workspace.History.Current);
    }

    [Fact]
    public async Task A_persons_card_carries_the_relations_they_are_an_end_of()
    {
        Harness harness = Harness.Build();
        harness.Dossiers.Npcs = new NpcRoster { Npcs = new[] { Wenna } };
        harness.Dossiers.Party = new PartyDossier { Characters = new[] { Maren } };
        var protects = new RelationLine { Kind = "protects", Phrase = "protects", OtherId = "maren", OtherLabel = "Maren" };
        harness.Content.Relations["wenna-brask"] = new[] { protects };
        await harness.Workspace.InitialiseAsync(CancellationToken.None);

        harness.Workspace.OpenReference("wenna-brask");
        Assert.Same(protects, Assert.Single(harness.Workspace.ReferenceCard.Relations));

        harness.Workspace.OpenReference("maren");
        Assert.Empty(harness.Workspace.ReferenceCard.Relations);
    }

    [Fact]
    public async Task A_relation_note_is_read_on_the_floor_it_was_written_on()
    {
        Harness harness = await Harness.Started();
        harness.Workspace.NavigateTo(Landing);

        harness.CrossRefs.ScopedTo.Clear();
        harness.Workspace.ResolveRefsOn("They met in area 6d.", "data_dossiers_level_2");
        harness.Workspace.ResolveRefsOn("They met in the city.", null);

        Assert.Equal(FloorTwoThenNone, harness.CrossRefs.ScopedTo);
    }

    [Fact]
    public async Task A_cards_prose_is_read_on_the_floor_it_belongs_to()
    {
        // An entity on Level 2 means its own floor's "area 6d", even while the panel shows Level 1.
        Harness harness = await Harness.Started();
        harness.Content.Cards["level-two-thing"] = new ReferenceCard
        {
            NodeId = "level-two-thing",
            Label = "A thing on level 2",
            Kind = CrossRefKind.Item,
            LevelNodeId = "data_dossiers_level_2",
        };
        harness.Workspace.NavigateTo(Landing);
        harness.Workspace.OpenReference("level-two-thing");

        harness.CrossRefs.ScopedTo.Clear();
        harness.Workspace.ResolveCardRefs("It came from area 6d.");
        Assert.Equal("data_dossiers_level_2", Assert.Single(harness.CrossRefs.ScopedTo));

        // A card that belongs to no floor reads on the shown one, as the panel does.
        harness.Workspace.OpenReference("data_statblocks_monsters_bugbear");
        harness.CrossRefs.ScopedTo.Clear();
        harness.Workspace.ResolveCardRefs("It came from area 6d.");
        Assert.Equal(Level1, Assert.Single(harness.CrossRefs.ScopedTo));
    }

    [Fact]
    public async Task Every_shared_mutation_notifies_the_tabs()
    {
        Harness harness = await Harness.Started();
        int changes = 0;
        harness.Workspace.Changed += () => changes++;

        harness.Workspace.NavigateTo(Landing);
        harness.Workspace.SelectRegion("r-unlinked");

        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task The_encounter_strip_is_the_shown_areas_creature_list()
    {
        Harness harness = await Harness.Started();
        harness.Content.Briefings[Landing] = new RoomBriefing
        {
            RoomId = Landing,
            Label = "Undercroft Landing",
            Occupants = new[]
            {
                new OccupantEntry
                {
                    NodeId = "data_statblocks_monsters_bandit",
                    Label = "Bandit",
                    MonsterRef = "data_statblocks_monsters_bandit",
                    Count = "4",
                },
            },
        };

        harness.Workspace.NavigateTo(Landing);
        EncounterMonster bandits = Assert.Single(harness.Workspace.Encounter);
        Assert.Equal("data_statblocks_monsters_bandit", bandits.NodeId);
        Assert.Equal(4, bandits.SuggestedCount);

        // Moving on leaves the last area's fight behind.
        harness.Workspace.NavigateTo(Pillars);
        Assert.Empty(harness.Workspace.Encounter);
    }

    // ---- Harness --------------------------------------------------------------------------

    private sealed class Harness
    {
        public FakeContentProjection Content { get; private set; }

        public FakeMapStore Annotations { get; private set; }

        public FakeVectorMapStore Maps { get; private set; }

        public FakeStatLibrary Stats { get; private set; }

        public FakeDossierStore Dossiers { get; private set; }

        public FakeCrossRefResolver CrossRefs { get; private set; }

        public SessionState Session { get; private set; }

        public DmWorkspace Workspace { get; private set; }

        public static Harness Build()
        {
            var content = new FakeContentProjection();
            content.Briefings[Landing] = new RoomBriefing { RoomId = Landing, Label = "Undercroft Landing" };
            content.Briefings[Pillars] = new RoomBriefing { RoomId = Pillars, Label = "Pillars" };
            content.Cards["data_statblocks_monsters_bugbear"] = new ReferenceCard { NodeId = "data_statblocks_monsters_bugbear", Label = "Bugbear", Kind = CrossRefKind.Monster };
            content.Cards["data_statblocks_monsters_bandit"] = new ReferenceCard { NodeId = "data_statblocks_monsters_bandit", Label = "Bandit", Kind = CrossRefKind.Monster };
            content.Cards[Pillars] = new ReferenceCard { NodeId = Pillars, Label = "Pillars", Kind = CrossRefKind.Area };
            content.Cards[Landing] = new ReferenceCard { NodeId = Landing, Label = "Undercroft Landing", Kind = CrossRefKind.Area };

            var stats = new FakeStatLibrary();
            stats.Monsters["data_statblocks_monsters_bandit"] = new StatBlock { NodeId = "data_statblocks_monsters_bandit", Name = "Bandit" };

            var maps = new FakeVectorMapStore();
            maps.Maps["level-1"] = new VectorMap
            {
                MapId = "level-1",
                Bounds = new MapBounds(0, 0, 1000, 800),
                Grid = new Grid { CellSizePx = 10 },
            };

            var annotations = new FakeMapStore();
            annotations.Definitions["level-1"] = new MapDefinition
            {
                MapId = "level-1",
                Regions = new[]
                {
                    Rect("r-entry-a", Landing, "Undercroft Landing", 0, 0, 40, 40),
                    Rect("r-entry-b", Landing, "Undercroft Landing east", 40, 0, 60, 20),
                    Rect("r-pillars", Pillars, "Pillars", 100, 100, 200, 200),
                    Rect("r-unlinked", string.Empty, "Storeroom", 300, 300, 340, 340),
                },
                Features = new[]
                {
                    new FeatureMarker { FeatureId = "m-pit", GraphNodeId = "data_dossiers_level_1_pit_trap", Kind = FeatureKind.Trap },
                },
                Objects = new[]
                {
                    new ObjectAnnotation { ObjectId = "door-secret", Kind = MapObjectKind.Door, IsSecret = true, IsConcealed = true },
                    new ObjectAnnotation { ObjectId = "door-double", Kind = MapObjectKind.Door, DoubleDoors = true },
                    new ObjectAnnotation { ObjectId = "door-open", Kind = MapObjectKind.Door, DefaultOpen = true },
                    new ObjectAnnotation { ObjectId = "statue", Kind = MapObjectKind.Decoration, IsConcealed = true },
                },
            };

            var session = new SessionState();
            var crossRefs = new FakeCrossRefResolver();
            var dossiers = new FakeDossierStore();
            // Both authored areas sit on Level 1, which is the floor a prose scan has to be told
            // about before an "area N" in it means anything.
            var level1 = new LevelDossier { LevelNodeId = Level1, Title = "Level 1: The Undercroft" };
            dossiers.LevelsByArea[Landing] = level1;
            dossiers.LevelsByArea[Pillars] = level1;

            // Where the screen opens; the start-area tests replace this.
            dossiers.Campaign = new CampaignDossier { StartAreaNodeId = Landing };

            var encounters = new EncounterSource(crossRefs, content, stats);
            return new Harness
            {
                Content = content,
                Annotations = annotations,
                Maps = maps,
                Stats = stats,
                Dossiers = dossiers,
                CrossRefs = crossRefs,
                Session = session,
                Workspace = new DmWorkspace(content, crossRefs, maps, annotations, dossiers, stats, encounters, session),
            };
        }

        public static async Task<Harness> Started()
        {
            Harness harness = Build();
            await harness.Workspace.InitialiseAsync(CancellationToken.None);
            return harness;
        }

        // Regions with a duplicated first region id would break the "two regions, one room" cases,
        // so each is a distinct axis-aligned rectangle.
        private static Region Rect(string id, string node, string label, double minX, double minY, double maxX, double maxY) =>
            new Region
            {
                RegionId = id,
                GraphNodeId = node,
                Label = label,
                Polygon = new[]
                {
                    new MapPoint(minX, minY),
                    new MapPoint(maxX, minY),
                    new MapPoint(maxX, maxY),
                    new MapPoint(minX, maxY),
                },
            };
    }
}
