using System.Collections.Generic;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Tests.Web;

namespace DungeonTable.Tests.Links;

/// <summary>
/// "Appears in": the areas whose creature list names something, and the areas whose prose links to
/// it, each once, in load order, and only what an author wrote.
/// </summary>
public sealed class BacklinksTests
{
    private const string Level1 = "data_dossiers_level_1";
    private const string Bandit = "data_statblocks_monsters_bandit";

    // Expected area orders, hoisted out of the assertions (CA1861).
    private static readonly string[] ListedThenMentioned = { "area_6", "area_8" };
    private static readonly string[] EveryProseField = { "glance", "detail", "creature note", "exit note" };

    private static readonly LevelDossier Level = new LevelDossier
    {
        LevelNodeId = Level1,
        Entities = new[] { new Entity { Id = "nib", Kind = EntityKind.Npc, Name = "Nib" } },
        Factions = new[] { new DossierBlock { Id = "wickfoot-goblins", Heading = "Wickfoot goblins" } },
    };

    [Fact]
    public void An_area_that_lists_and_mentions_something_is_one_entry_saying_both()
    {
        Backlinks backlinks = Build(
            Area("area_6", creatures: new[] { new AreaCreature { Ref = "nib" } }, readAloud: "[[nib|The goblin boss]] scowls."),
            Area("area_8", glance: "Grask hates [[nib]]."));

        IReadOnlyList<AreaLink> areas = backlinks.For("nib");

        Assert.Equal(ListedThenMentioned, areas.Select(area => area.NodeId).ToArray());
        Assert.Equal("in the room, mentioned", areas[0].Relation);
        Assert.Equal("mentioned", areas[1].Relation);
        Assert.Equal("Title of area_6", areas[0].Label);
    }

    [Fact]
    public void Every_prose_field_of_an_area_counts()
    {
        Backlinks backlinks = Build(
            Area("glance", glance: "[[bandit]]"),
            Area("detail", detail: "[[bandit]]"),
            Area("creature note", creatures: new[] { new AreaCreature { Ref = "nib", Note = "with a [[bandit]]" } }),
            Area("exit note", exits: new[] { new AreaExit { To = "area 1", Note = "a [[bandit]] guards it" } }));

        Assert.Equal(EveryProseField, backlinks.For(Bandit).Select(area => area.NodeId).ToArray());
    }

    [Fact]
    public void A_heading_a_name_the_matcher_would_find_and_a_broken_link_are_not_appearances()
    {
        Backlinks backlinks = Build(
            Area("area_1", glance: "The bandit captain waits.", heading: "[[bandit]]"),
            Area("area_2", readAloud: "[[bandit-captin]] and [[L9 area 1]]"));

        Assert.Empty(backlinks.For(Bandit));
        Assert.Empty(backlinks.For(string.Empty));
    }

    [Fact]
    public void A_faction_listed_as_a_creature_is_not_in_the_room()
    {
        Backlinks backlinks = Build(Area("area_6", creatures: new[] { new AreaCreature { Ref = "wickfoot-goblins" } }));

        Assert.Empty(backlinks.For("wickfoot-goblins"));
    }

    private static Backlinks Build(params AreaDossier[] areas)
    {
        var dossiers = new FakeDossierStore();
        foreach (AreaDossier area in areas)
        {
            dossiers.Areas[area.AreaNodeId] = area;
            dossiers.LevelsByArea[area.AreaNodeId] = Level;
        }

        dossiers.LevelNumbers[Level1] = 1;
        var stats = new FakeStatLibrary();
        stats.Monsters[Bandit] = new StatBlock { NodeId = Bandit, Name = "Bandit" };
        return Backlinks.Build(dossiers, LinkTargets.Build(dossiers, stats));
    }

    private static AreaDossier Area(
        string nodeId,
        IReadOnlyList<AreaCreature> creatures = null,
        IReadOnlyList<AreaExit> exits = null,
        string readAloud = "",
        string glance = "",
        string detail = "",
        string heading = "") => new AreaDossier
        {
            AreaNodeId = nodeId,
            Title = $"Title of {nodeId}",
            ReadAloud = readAloud,
            Glance = new[] { new DossierBlock { Heading = heading, Body = glance } },
            Detail = new[] { new DossierBlock { Body = detail } },
            Creatures = creatures ?? Array.Empty<AreaCreature>(),
            Exits = exits ?? Array.Empty<AreaExit>(),
        };
}
