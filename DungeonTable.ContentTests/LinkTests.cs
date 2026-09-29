using DungeonTable.ContentTests.Rules;

namespace DungeonTable.ContentTests;

/// <summary>
/// The link rules, over every dossier document at once: every <c>[[…]]</c> resolves, no target name
/// means two things, links sit only where they are shown as links, every creature, count, exit, entity,
/// relation and contact an author writes can be used, and every block has a heading and a body.
/// </summary>
public sealed class LinkTests
{
    [Fact]
    public void Every_link_resolves()
    {
        Report.None(LinkAudit.BrokenLinks(ContentRoot.Dossiers, ContentRoot.Targets), "Broken links");
    }

    [Fact]
    public void No_link_target_means_two_things()
    {
        Report.None(ContentRoot.Targets.Collisions(), "Ambiguous link targets");
    }

    [Fact]
    public void Links_sit_only_in_fields_shown_as_prose()
    {
        Report.None(LinkAudit.MisplacedLinks(ContentRoot.Dossiers), "Misplaced links");
    }

    [Fact]
    public void Every_creature_an_area_lists_is_a_creature_that_is_loaded()
    {
        Report.None(LinkAudit.BrokenCreatures(ContentRoot.Dossiers, ContentRoot.Targets), "Broken creature entries");
    }

    [Fact]
    public void Every_creature_count_reads()
    {
        Report.None(LinkAudit.BadCounts(ContentRoot.Dossiers), "Unreadable counts");
    }

    [Fact]
    public void Every_exit_leads_to_an_area_or_waits_for_its_floor()
    {
        Report.None(LinkAudit.BrokenExits(ContentRoot.Dossiers, ContentRoot.Targets), "Broken exits");
    }

    [Fact]
    public void Every_entity_is_usable()
    {
        Report.None(LinkAudit.BadEntities(ContentRoot.Dossiers, ContentRoot.Targets, ContentRoot.Stats), "Unusable entities");
    }

    [Fact]
    public void Every_npc_that_names_a_stat_block_names_one_the_library_has()
    {
        Report.None(LinkAudit.BadNpcBlocks(ContentRoot.Dossiers, ContentRoot.Targets, ContentRoot.Stats), "NPCs with a stat block that cannot be used");
    }

    [Fact]
    public void Every_relation_and_every_contacts_ref_can_be_drawn()
    {
        Report.None(LinkAudit.BadRelations(ContentRoot.Dossiers, ContentRoot.Targets), "Relations that cannot be drawn");
    }

    [Fact]
    public void Every_block_has_a_heading_and_a_body()
    {
        Report.None(LinkAudit.EmptyBlocks(ContentRoot.Dossiers), "Empty blocks");
    }
}
