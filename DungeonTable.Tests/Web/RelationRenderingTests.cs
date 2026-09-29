using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Web.Components.Dm;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Renders relations where the DM meets them: on a card in the drawer, as a room's "Involved with",
/// and as the contacts on an NPC's card and the cast of a story chapter that open someone's card.
/// </summary>
public sealed class RelationRenderingTests
{
    private static readonly RelationLine Rival = new RelationLine
    {
        Kind = "rival",
        Phrase = "rival of",
        OtherId = "grask",
        OtherLabel = "Grask",
        OtherKind = CrossRefKind.Npc,
        OtherWhere = new[] { "4" },
        Note = "An old grudge; see [[area 4]].",
        Floor = "data_dossiers_level_1",
    };

    private static readonly RelationLine Hunted = new RelationLine
    {
        Kind = "hunts",
        Phrase = "hunted by",
        OtherId = "wenna-brask",
        OtherLabel = "Wenna Brask",
        OtherKind = CrossRefKind.Npc,
        Secret = true,
    };

    [Fact]
    public async Task A_relation_line_reads_phrase_other_where_badge_and_note_resolved_on_its_floor()
    {
        var floors = new List<string>();

        string html = await Render<RelationLines>(new()
        {
            [nameof(RelationLines.Lines)] = new[] { Rival, Hunted },
            [nameof(RelationLines.OnOpenReference)] = EventCallback.Factory.Create<string>(this, _ => { }),
            [nameof(RelationLines.ResolveRefsOn)] = (Func<string, string, IReadOnlyList<CrossRef>>)((text, floor) =>
            {
                floors.Add(floor);
                return Array.Empty<CrossRef>();
            }),
        });

        Assert.Contains("<span class=\"relation-phrase\">rival of</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Grask</button>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"relation-where\" title=\"The rooms that list them\">(4)</span>", html, StringComparison.Ordinal);
        Assert.Contains("<li class=\"relation-line secret\">", html, StringComparison.Ordinal);
        Assert.Contains(">DM</span>", html, StringComparison.Ordinal);
        Assert.Equal(1, html.Split("class=\"relation-note\"").Length - 1);
        Assert.Equal("data_dossiers_level_1", Assert.Single(floors));
    }

    [Fact]
    public async Task Without_a_resolver_a_notes_link_is_reduced_to_its_words()
    {
        string html = await Render<RelationLines>(new() { [nameof(RelationLines.Lines)] = new[] { Rival } });

        Assert.Contains("An old grudge; see area 4.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("[[", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"relation-other\">Grask</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entitys_card_lists_its_relations_before_the_stat_block_it_uses()
    {
        string html = await Render<ReferenceDrawer>(new()
        {
            [nameof(ReferenceDrawer.Card)] = new ReferenceCard
            {
                NodeId = "nib",
                Label = "Nib",
                Kind = CrossRefKind.Npc,
                Summary = "Leads the goblins.",
                StatBlockId = "goblin-node",
                Relations = new[] { Rival },
            },
            [nameof(ReferenceDrawer.StatBlock)] = new StatBlock { NodeId = "goblin-node", Name = "Goblin" },
        });

        int relations = html.IndexOf("class=\"reference-relations\"", StringComparison.Ordinal);
        int uses = html.IndexOf("Uses the Goblin stat block", StringComparison.Ordinal);
        Assert.True(relations > 0 && uses > relations, html);
    }

    [Fact]
    public async Task A_persons_card_leaves_out_the_relations_their_own_contacts_already_list()
    {
        var wenna = new NpcDossier
        {
            Id = "wenna-brask",
            Name = "Wenna Brask",
            Contacts = new[] { new CastMember { Name = "Tam Brask", Ref = "tam-brask", Rel = "protects" } },
        };
        var own = new RelationLine { Kind = "protects", Phrase = "protects", OtherId = "tam-brask", OtherLabel = "Tam Brask", FromContactsOf = new[] { "wenna-brask" } };

        string html = await Render<ReferenceDrawer>(new()
        {
            [nameof(ReferenceDrawer.Card)] = new ReferenceCard { NodeId = wenna.Id, Label = wenna.Name, Kind = CrossRefKind.Npc, Relations = new[] { own, Hunted } },
            [nameof(ReferenceDrawer.Npc)] = wenna,
        });

        Assert.Contains("hunted by", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<span class=\"relation-phrase\">protects</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_room_lists_who_its_occupants_are_involved_with()
    {
        var briefing = new RoomBriefing
        {
            RoomId = "area-6",
            Involvements = new[] { new Involvement { SubjectId = "nib", SubjectLabel = "Nib", Lines = new[] { Rival } } },
        };

        string html = await Render<RoomBriefingPanel>(new() { [nameof(RoomBriefingPanel.Briefing)] = briefing });

        Assert.Contains("<h3>Involved with</h3>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"involvement-subject\">Nib</span>", html, StringComparison.Ordinal);
        Assert.Contains("rival of", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_room_with_no_involvements_draws_no_section()
    {
        string html = await Render<RoomBriefingPanel>(new() { [nameof(RoomBriefingPanel.Briefing)] = new RoomBriefing { RoomId = "area-6" } });

        Assert.DoesNotContain("Involved with", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_contact_or_cast_row_with_a_ref_opens_their_card()
    {
        var callback = EventCallback.Factory.Create<string>(this, _ => { });
        var wenna = new NpcDossier
        {
            Id = "wenna-brask",
            Name = "Wenna Brask",
            Contacts = new[]
            {
                new CastMember { Name = "Tam Brask", Ref = "tam-brask" },
                new CastMember { Name = "Old Hobb" },
            },
        };

        string card = await Render<NpcCard>(new()
        {
            [nameof(NpcCard.Npc)] = wenna,
            [nameof(NpcCard.StartOpen)] = true,
            [nameof(NpcCard.OnOpenReference)] = callback,
        });
        string story = await Render<StoryPanel>(new()
        {
            [nameof(StoryPanel.Story)] = new StoryLibrary
            {
                Chapters = new[] { new StoryDossier { Title = "Why the bell fell silent", Cast = new[] { new CastMember { Name = "Grask", Ref = "grask" } } } },
            },
            [nameof(StoryPanel.OnOpenReference)] = callback,
        });

        Assert.Contains("title=\"Open their card\">Tam Brask</button>", card, StringComparison.Ordinal);
        Assert.DoesNotContain(">Old Hobb</button>", card, StringComparison.Ordinal);
        Assert.Contains("title=\"Open their card\">Grask</button>", story, StringComparison.Ordinal);
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
