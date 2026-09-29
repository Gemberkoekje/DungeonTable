using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>
/// Proves the link rules catch what they exist to catch, on content built to break them. Content that
/// passes cannot show that a rule works: a rule that checked nothing would pass it too.
/// </summary>
public sealed class LinkAuditTests
{
    private const string Level1 = "data_dossiers_level_1";
    private const string Area2a = "data_dossiers_level_1_area_2a";

    [Fact]
    public void A_link_that_resolves_is_not_reported()
    {
        FakeDossierStore dossiers = Content(glanceBody: "A door leads to [[area 2a]], guarded by a [[bandit]].");

        Assert.Empty(LinkAudit.BrokenLinks(dossiers, Targets(dossiers)));
    }

    [Fact]
    public void A_link_that_names_nothing_is_reported_with_where_it_is()
    {
        FakeDossierStore dossiers = Content(glanceBody: "Guarded by a [[bandit-captin|captain]].");

        string broken = Assert.Single(LinkAudit.BrokenLinks(dossiers, Targets(dossiers)));

        Assert.Contains($"area {Area2a}.Glance[0].Body", broken, StringComparison.Ordinal);
        Assert.Contains("[[bandit-captin]]", broken, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bare_area_link_in_campaign_wide_prose_is_reported_as_needing_a_floor()
    {
        FakeDossierStore dossiers = Content(npcSummary: "She drinks in [[area 2a]].");

        string broken = Assert.Single(LinkAudit.BrokenLinks(dossiers, Targets(dossiers)));

        Assert.Contains("npcs.json", broken, StringComparison.Ordinal);
        Assert.Contains("[[L1 area 2a]]", broken, StringComparison.Ordinal);
    }

    [Fact]
    public void An_area_link_that_names_its_floor_resolves_from_campaign_wide_prose()
    {
        FakeDossierStore dossiers = Content(npcSummary: "She drinks in [[L1 area 2a|the reliefs room]].");

        Assert.Empty(LinkAudit.BrokenLinks(dossiers, Targets(dossiers)));
    }

    [Fact]
    public void A_link_in_a_field_not_rendered_as_prose_is_reported()
    {
        FakeDossierStore dossiers = Content(glanceHeading: "The [[bandit]] camp");

        string misplaced = Assert.Single(LinkAudit.MisplacedLinks(dossiers));

        Assert.Contains($"area {Area2a}.Glance[0].Heading", misplaced, StringComparison.Ordinal);
        Assert.Empty(LinkAudit.BrokenLinks(dossiers, Targets(dossiers)));
    }

    [Fact]
    public void Brackets_that_are_not_a_link_are_not_misplaced_markup()
    {
        FakeDossierStore dossiers = Content(glanceHeading: "An unclosed [[ in a heading");

        Assert.Empty(LinkAudit.MisplacedLinks(dossiers));
    }

    [Fact]
    public void Creatures_that_name_a_creature_a_person_or_a_character_are_not_reported()
    {
        FakeDossierStore dossiers = Content(creatures: new[]
        {
            new AreaCreature { Ref = "bandit", Count = "4" },
            new AreaCreature { Ref = "bell-warden" },
            new AreaCreature { Ref = "wenna-brask", Count = "1d4+1" },
        });

        Assert.Empty(LinkAudit.BrokenCreatures(dossiers, Targets(dossiers)));
        Assert.Empty(LinkAudit.BadCounts(dossiers));
    }

    [Fact]
    public void A_creature_that_names_nothing_or_no_creature_is_reported()
    {
        FakeDossierStore dossiers = Content(creatures: new[]
        {
            new AreaCreature { Ref = "bandit-captin" },
            new AreaCreature { Ref = "wickfoot-goblins" },
            new AreaCreature { Ref = "area 2a" },
        });

        IReadOnlyList<string> broken = LinkAudit.BrokenCreatures(dossiers, Targets(dossiers));

        Assert.Equal(3, broken.Count);
        Assert.Contains($"area {Area2a}.Creatures[0]", broken[0], StringComparison.Ordinal);
        Assert.Contains("'wickfoot-goblins' names a faction", broken[1], StringComparison.Ordinal);
        Assert.Contains("'area 2a' names an area", broken[2], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("four")]
    [InlineData("0")]
    [InlineData("100")]
    public void A_count_that_does_not_read_is_reported(string count)
    {
        FakeDossierStore dossiers = Content(creatures: new[] { new AreaCreature { Ref = "bandit", Count = count } });

        string bad = Assert.Single(LinkAudit.BadCounts(dossiers));

        Assert.Contains($"'{count}'", bad, StringComparison.Ordinal);
    }

    [Fact]
    public void An_exit_to_an_area_or_to_a_floor_not_written_yet_is_not_reported()
    {
        FakeDossierStore dossiers = Content(exits: new[] { new AreaExit { To = "area 2a" }, new AreaExit { To = "L2 area 1" } });

        Assert.Empty(LinkAudit.BrokenExits(dossiers, Targets(dossiers)));
    }

    [Fact]
    public void An_exit_to_no_area_or_to_something_else_is_reported()
    {
        FakeDossierStore dossiers = Content(exits: new[]
        {
            new AreaExit { To = "area 99" },
            new AreaExit { To = "bandit" },
            new AreaExit { To = string.Empty },
        });

        IReadOnlyList<string> broken = LinkAudit.BrokenExits(dossiers, Targets(dossiers));

        Assert.Equal(3, broken.Count);
        Assert.Contains("no area 99", broken[0], StringComparison.Ordinal);
        Assert.Contains("'bandit' names a creature, not an area", broken[1], StringComparison.Ordinal);
        Assert.Contains($"area {Area2a}.Exits[2]", broken[2], StringComparison.Ordinal);
    }

    [Fact]
    public void A_usable_entity_is_not_reported()
    {
        FakeDossierStore dossiers = Content();

        Assert.Empty(LinkAudit.BadEntities(dossiers, Targets(dossiers), Stats()));
    }

    [Fact]
    public void An_entity_a_link_or_card_could_not_use_is_reported()
    {
        FakeDossierStore dossiers = Content(entities: new[]
        {
            new Entity { Id = "Bell Warden", Kind = EntityKind.Creature, Name = "the Bell-Warden" },
            new Entity { Id = "nameless", Kind = EntityKind.Npc },
            new Entity { Id = "kindless", Name = "Kindless" },
            new Entity { Id = "shadow", Kind = EntityKind.Creature, Name = "A shadow", StatBlock = "bell-warden" },
        });

        IReadOnlyList<string> bad = LinkAudit.BadEntities(dossiers, Targets(dossiers), Stats());

        Assert.Equal(4, bad.Count);
        Assert.Contains("'bell-warden'", bad[0], StringComparison.Ordinal);
        Assert.Contains("no name", bad[1], StringComparison.Ordinal);
        Assert.Contains("no kind", bad[2], StringComparison.Ordinal);
        Assert.Contains("never uses another individual", bad[3], StringComparison.Ordinal);
    }

    [Fact]
    public void An_npc_that_uses_an_extracted_stat_block_or_none_is_not_reported()
    {
        FakeDossierStore dossiers = Content();
        dossiers.Npcs = new NpcRoster
        {
            Npcs = new[]
            {
                new NpcDossier { Id = "brother-aldous", Name = "Brother Aldous", StatBlock = "bandit" },
                new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask" },
            },
        };

        Assert.Empty(LinkAudit.BadNpcBlocks(dossiers, Targets(dossiers), Stats()));
    }

    [Theory]
    [InlineData("bandit-captin")]
    [InlineData("bell-warden")]
    [InlineData("wickfoot-goblins")]
    [InlineData("area 2a")]
    public void An_npc_whose_stat_block_is_not_an_extracted_one_is_reported(string statBlock)
    {
        FakeDossierStore dossiers = Content();
        dossiers.Npcs = new NpcRoster
        {
            Npcs = new[] { new NpcDossier { Id = "brother-aldous", Name = "Brother Aldous", StatBlock = statBlock } },
        };

        string bad = Assert.Single(LinkAudit.BadNpcBlocks(dossiers, Targets(dossiers), Stats()));

        Assert.Contains($"npcs.json 'brother-aldous': its stat block '{statBlock}'", bad, StringComparison.Ordinal);
    }

    [Fact]
    public void A_link_in_an_entity_description_or_a_creature_note_is_prose()
    {
        FakeDossierStore dossiers = Content(
            creatures: new[] { new AreaCreature { Ref = "bandit", Note = "guarding [[area 2a]]" } },
            entities: new[] { new Entity { Id = "chief", Kind = EntityKind.Npc, Name = "The chief", Description = "Rules [[area 2a]] and [[area 99]]." } });

        Assert.Empty(LinkAudit.MisplacedLinks(dossiers));
        string broken = Assert.Single(LinkAudit.BrokenLinks(dossiers, Targets(dossiers)));
        Assert.Contains("Entities[0].Description", broken, StringComparison.Ordinal);
    }

    [Fact]
    public void Relations_between_particular_things_are_not_reported()
    {
        FakeDossierStore dossiers = Content(relations: new[]
        {
            new Relation { A = "bell-warden", Rel = "serves", B = "wickfoot-goblins", Note = "Guards [[area 2a]]." },
            new Relation { A = "wickfoot-goblins", Rel = "located-in", B = "area 2a" },
        });
        dossiers.Relations = new RelationList { Relations = new[] { new Relation { A = "wenna-brask", Rel = "fears", B = "L1 area 2a" } } };

        Assert.Empty(LinkAudit.BadRelations(dossiers, Targets(dossiers)));
        Assert.Empty(LinkAudit.BrokenLinks(dossiers, Targets(dossiers)));
        Assert.Empty(LinkAudit.MisplacedLinks(dossiers));
    }

    [Theory]
    [InlineData("bell-warden", "friend", "wickfoot-goblins", "'friend' is not a relation kind")]
    [InlineData("bell-warden", "related", "wickfoot-goblins", "'related' is for the book index only")]
    [InlineData("bell-warden", "fears", "nobody-at-all", "its b end [[nobody-at-all]] names nothing")]
    [InlineData("bandit", "fears", "wickfoot-goblins", "its a end 'bandit' names a stat block or a spell")]
    [InlineData("bell-warden", "hunts", "L9 area 1", "its b end 'L9 area 1' is on a floor nobody has authored yet")]
    [InlineData("wickfoot-goblins", "rival", "wickfoot-goblins", "both ends are 'wickfoot-goblins'")]
    public void A_relation_that_cannot_be_drawn_is_reported(string a, string rel, string b, string expected)
    {
        FakeDossierStore dossiers = Content(relations: new[] { new Relation { A = a, Rel = rel, B = b } });

        string bad = Assert.Single(LinkAudit.BadRelations(dossiers, Targets(dossiers)));

        Assert.Contains($"level {Level1}.Relations[0]", bad, StringComparison.Ordinal);
        Assert.Contains(expected, bad, StringComparison.Ordinal);
    }

    [Fact]
    public void A_contacts_rel_needs_a_ref_and_a_ref_on_a_contact_or_cast_row_needs_a_card()
    {
        FakeDossierStore dossiers = Content();
        dossiers.Npcs = new NpcRoster
        {
            Npcs = new[]
            {
                new NpcDossier
                {
                    Id = "wenna-brask",
                    Name = "Wenna Brask",
                    Contacts = new[]
                    {
                        new CastMember { Name = "Old Hobb", Rel = "serves" },
                        new CastMember { Name = "Tam", Ref = "tam-brask", Rel = "protects" },
                        new CastMember { Name = "The warden", Ref = "bell-warden", Rel = "fears" },
                    },
                },
            },
        };
        dossiers.Story = new StoryLibrary
        {
            Chapters = new[] { new StoryDossier { Title = "Why the bell fell silent", Cast = new[] { new CastMember { Name = "Brother Aldous", Ref = "brother-aldous" } } } },
        };

        IReadOnlyList<string> bad = LinkAudit.BadRelations(dossiers, Targets(dossiers));

        Assert.Equal(4, bad.Count);
        Assert.Contains(bad, fault => fault.Contains("Contacts[1]: its b end", StringComparison.Ordinal));
        Assert.Contains(bad, fault => fault.Contains("Contacts[0]: it has a rel ('serves') but no ref", StringComparison.Ordinal));
        Assert.Contains(bad, fault => fault.Contains("Contacts[1]: its ref [[tam-brask]]", StringComparison.Ordinal));
        Assert.Contains(bad, fault => fault.Contains("story.json 'Why the bell fell silent'.Cast[0]: its ref [[brother-aldous]]", StringComparison.Ordinal));
    }

    [Fact]
    public void A_link_in_a_contacts_rel_or_ref_is_misplaced()
    {
        FakeDossierStore dossiers = Content();
        dossiers.Npcs = new NpcRoster
        {
            Npcs = new[] { new NpcDossier { Id = "wenna-brask", Name = "Wenna", Contacts = new[] { new CastMember { Name = "X", Ref = "[[wickfoot-goblins]]" } } } },
        };

        Assert.Contains(LinkAudit.MisplacedLinks(dossiers), misplaced => misplaced.Contains("Contacts[0].Ref", StringComparison.Ordinal));
    }

    [Fact]
    public void The_walk_reads_a_floors_prose_on_that_floor_and_campaign_wide_prose_on_none()
    {
        // A walk that silently found nothing would make every rule over the texts pass vacuously.
        FakeDossierStore dossiers = Content();

        IReadOnlyList<ContentText> texts = LinkAudit.Texts(dossiers).ToArray();

        Assert.Contains(texts, text => text.Owner == typeof(DossierBlock) && text.Floor == Level1 && text.Where.StartsWith($"area {Area2a}.Glance[0]", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.Owner == typeof(NpcDossier) && text.Property == nameof(NpcDossier.Summary) && text.Floor.Length == 0);
    }

    [Fact]
    public void A_block_with_a_heading_and_a_body_is_not_reported()
    {
        Assert.Empty(LinkAudit.EmptyBlocks(Content()));
    }

    [Theory]
    [InlineData("", "Plain prose.", "Glance[0].Heading")]
    [InlineData("Reliefs", "  ", "Glance[0].Body")]
    [InlineData(null, "Plain prose.", "Glance[0].Heading")]
    public void A_block_with_no_heading_or_no_body_is_reported_with_where_it_is(string heading, string body, string expected)
    {
        FakeDossierStore dossiers = Content(glanceHeading: heading, glanceBody: body);

        string empty = Assert.Single(LinkAudit.EmptyBlocks(dossiers));

        Assert.Contains($"area {Area2a}.{expected}", empty, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_block_in_a_campaign_wide_document_is_reported_too()
    {
        FakeDossierStore dossiers = Content();
        dossiers.Npcs = new NpcRoster
        {
            Npcs = new[] { new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask", Knows = new[] { new DossierBlock { Heading = "Tam went down at dusk" } } } },
        };

        string empty = Assert.Single(LinkAudit.EmptyBlocks(dossiers));

        Assert.Contains("npcs.json.Npcs[0].Knows[0].Body", empty, StringComparison.Ordinal);
    }

    private static FakeDossierStore Content(
        string glanceBody = "Plain prose.",
        string glanceHeading = "Reliefs",
        string npcSummary = "A smith.",
        IReadOnlyList<AreaCreature> creatures = null,
        IReadOnlyList<AreaExit> exits = null,
        IReadOnlyList<Entity> entities = null,
        IReadOnlyList<Relation> relations = null)
    {
        var dossiers = new FakeDossierStore();
        var level = new LevelDossier
        {
            LevelNodeId = Level1,
            Title = "Level 1: The Undercroft",
            Entities = entities ?? new[] { new Entity { Id = "bell-warden", Kind = EntityKind.Creature, Name = "the Bell-Warden", StatBlock = "bandit" } },
            Factions = new[] { new DossierBlock { Id = "wickfoot-goblins", Heading = "Wickfoot goblins", Body = "A band of goblins." } },
            Relations = relations ?? Array.Empty<Relation>(),
        };
        dossiers.Areas[Area2a] = new AreaDossier
        {
            AreaNodeId = Area2a,
            Title = "Demon Reliefs (Level 1, Area 2a)",
            AreaNumber = 2,
            Glance = new[] { new DossierBlock { Heading = glanceHeading, Body = glanceBody } },
            Creatures = creatures ?? Array.Empty<AreaCreature>(),
            Exits = exits ?? Array.Empty<AreaExit>(),
        };
        dossiers.LevelsByArea[Area2a] = level;
        dossiers.LevelNumbers[Level1] = 1;
        dossiers.Npcs = new NpcRoster
        {
            Npcs = new[] { new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask", Summary = npcSummary } },
        };
        return dossiers;
    }

    private static LinkTargets Targets(FakeDossierStore dossiers) => LinkTargets.Build(dossiers, Stats());

    private static FakeStatLibrary Stats()
    {
        var stats = new FakeStatLibrary();
        stats.Monsters["data_statblocks_monsters_bandit"] = new StatBlock { NodeId = "data_statblocks_monsters_bandit", Name = "Bandit" };
        return stats;
    }
}
