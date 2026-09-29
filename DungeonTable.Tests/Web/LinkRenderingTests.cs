using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Tests.Battles;
using DungeonTable.Web.Components.Dm;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Renders the prose fields that used to print their text raw (the read-aloud box, the roster,
/// party and session summaries, an NPC's appearance) and checks that an authored link in each now
/// reaches the page as a link rather than as brackets. Also renders the drawer with a person in it.
/// </summary>
public sealed class LinkRenderingTests
{
    private const string Prose = "Four [[bandit|human bandits]] play cards.";

    private static readonly MarkupCrossRefResolver Resolver = new MarkupCrossRefResolver(
        LinkTargets.Build(new FakeDossierStore(), Stats()));

    private static readonly Func<string, IReadOnlyList<CrossRef>> ResolveRefs = text => Resolver.Resolve(text);

    [Fact]
    public async Task The_read_aloud_box_renders_a_link()
    {
        string html = await Render<RoomBriefingPanel>(new()
        {
            [nameof(RoomBriefingPanel.Dossier)] = new AreaDossier { AreaNodeId = "area", ReadAloud = Prose },
            [nameof(RoomBriefingPanel.ResolveRefs)] = ResolveRefs,
        });

        AssertLinked(html);
        Assert.Contains("<blockquote class=\"read-aloud\">Four <button", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_npc_rosters_summary_renders_a_link()
    {
        string html = await Render<NpcPanel>(new()
        {
            [nameof(NpcPanel.Roster)] = new NpcRoster { Summary = Prose, Npcs = new[] { new NpcDossier { Id = "a", Name = "A" } } },
            [nameof(NpcPanel.ResolveRefs)] = ResolveRefs,
        });

        AssertLinked(html);
    }

    [Fact]
    public async Task An_npcs_appearance_renders_a_link()
    {
        string html = await Render<NpcCard>(new()
        {
            [nameof(NpcCard.Npc)] = new NpcDossier { Id = "a", Name = "A", Appearance = Prose },
            [nameof(NpcCard.StartOpen)] = true,
            [nameof(NpcCard.ResolveRefs)] = ResolveRefs,
        });

        AssertLinked(html);
    }

    [Fact]
    public async Task The_party_summary_renders_a_link()
    {
        string html = await Render<PartyPanel>(new()
        {
            [nameof(PartyPanel.Party)] = new PartyDossier { Summary = Prose, Characters = new[] { new CharacterDossier { Id = "a", Name = "A" } } },
            [nameof(PartyPanel.ResolveRefs)] = ResolveRefs,
        });

        AssertLinked(html);
    }

    [Fact]
    public async Task The_session_logs_summary_renders_a_link()
    {
        string html = await Render<SessionPanel>(new()
        {
            [nameof(SessionPanel.Log)] = new SessionLog { Summary = Prose, Sessions = new[] { new SessionPlan { Id = "s", Title = "S" } } },
            [nameof(SessionPanel.ResolveRefs)] = ResolveRefs,
        });

        AssertLinked(html);
    }

    [Fact]
    public async Task The_drawer_shows_a_persons_whole_card_opened()
    {
        var wenna = new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask", Summary = "Runs the mill by the ford." };

        string html = await Render<ReferenceDrawer>(new()
        {
            [nameof(ReferenceDrawer.Card)] = new ReferenceCard { NodeId = wenna.Id, Label = wenna.Name, Kind = CrossRefKind.Npc },
            [nameof(ReferenceDrawer.Npc)] = wenna,
        });

        // The summary sits inside the card's disclosure, so it only renders when the card is open.
        Assert.Contains("Runs the mill by the ford.", html, StringComparison.Ordinal);
        Assert.Contains("npc-card", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_drawer_shows_an_individuals_description_whereabouts_and_then_the_block_it_uses()
    {
        var card = new ReferenceCard
        {
            NodeId = "bell-warden",
            Label = "the Bell-Warden",
            Kind = CrossRefKind.Monster,
            Summary = "Guards the nave.",
            StatBlockId = "data_statblocks_monsters_gargoyle",
            Areas = new[] { new AreaLink { NodeId = "area-4", Label = "Bell-Founder's Workshop", Relation = "in the room" } },
        };

        string html = await Render<ReferenceDrawer>(new()
        {
            [nameof(ReferenceDrawer.Card)] = card,
            [nameof(ReferenceDrawer.StatBlock)] = new StatBlock { NodeId = "data_statblocks_monsters_gargoyle", Name = "Gargoyle" },
            [nameof(ReferenceDrawer.OnOpenReference)] = EventCallback.Factory.Create<string>(this, _ => { }),
        });

        int summary = html.IndexOf("Guards the nave.", StringComparison.Ordinal);
        int whereabouts = html.IndexOf(">Bell-Founder&#x27;s Workshop</button>", StringComparison.Ordinal);
        int uses = html.IndexOf("Uses the Gargoyle stat block", StringComparison.Ordinal);
        int block = html.IndexOf("class=\"stat-block\"", StringComparison.Ordinal);
        Assert.True(summary >= 0 && whereabouts > summary && uses > whereabouts && block > uses, html);
    }

    [Fact]
    public async Task A_stat_blocks_own_card_shows_the_block_alone()
    {
        string html = await Render<ReferenceDrawer>(new()
        {
            [nameof(ReferenceDrawer.Card)] = new ReferenceCard { NodeId = "bandit-node", Label = "Bandit", Kind = CrossRefKind.Monster, Summary = "A summary." },
            [nameof(ReferenceDrawer.StatBlock)] = new StatBlock { NodeId = "bandit-node", Name = "Bandit" },
        });

        Assert.Contains("class=\"stat-block\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Uses the", html, StringComparison.Ordinal);
        Assert.DoesNotContain("A summary.", html, StringComparison.Ordinal);
    }

    private static void AssertLinked(string html)
    {
        Assert.Contains(">human bandits</button>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("[[", html, StringComparison.Ordinal);
    }

    private static FakeStatLibrary Stats()
    {
        var stats = new FakeStatLibrary();
        stats.Monsters["data_statblocks_monsters_bandit"] = new StatBlock { NodeId = "data_statblocks_monsters_bandit", Name = "Bandit" };
        return stats;
    }

    private static async Task<string> Render<TComponent>(Dictionary<string, object> parameters)
        where TComponent : IComponent
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
