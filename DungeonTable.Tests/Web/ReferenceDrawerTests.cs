using System.Collections.Generic;
using System.Threading.Tasks;
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
/// Renders the reference drawer to HTML: an individual's card shows the stat block it uses beneath
/// everything said about it, whether it is an entity a level declares or one of the campaign's NPCs.
/// </summary>
public sealed class ReferenceDrawerTests
{
    private const string Acolyte = "data_statblocks_monsters_acolyte";
    private const string Gargoyle = "data_statblocks_monsters_gargoyle";

    private static readonly NpcDossier Aldous = new NpcDossier
    {
        Id = "brother-aldous",
        Name = "Brother Aldous",
        Summary = "The chapel's acolyte, in debt to a bugbear.",
    };

    [Fact]
    public async Task An_npc_who_fights_shows_the_block_they_use_beneath_their_card()
    {
        string html = await Render(
            new ReferenceCard { NodeId = Aldous.Id, Label = Aldous.Name, Kind = CrossRefKind.Npc, StatBlockId = Acolyte },
            Aldous,
            new StatBlock { NodeId = Acolyte, Name = "Acolyte" });

        int card = html.IndexOf("The chapel&#x27;s acolyte, in debt to a bugbear.", StringComparison.Ordinal);
        int block = html.IndexOf("Uses the Acolyte stat block", StringComparison.Ordinal);
        Assert.True(card >= 0, "the NPC's own card is shown");
        Assert.True(block > card, "the block comes beneath the NPC's card");
    }

    [Fact]
    public async Task An_npc_who_uses_no_block_shows_only_their_card()
    {
        string html = await Render(
            new ReferenceCard { NodeId = Aldous.Id, Label = Aldous.Name, Kind = CrossRefKind.Npc },
            Aldous,
            new StatBlock());

        Assert.Contains("The chapel&#x27;s acolyte", html, StringComparison.Ordinal);
        Assert.DoesNotContain("reference-uses", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_npcs_block_the_library_has_not_extracted_says_so()
    {
        string html = await Render(
            new ReferenceCard { NodeId = Aldous.Id, Label = Aldous.Name, Kind = CrossRefKind.Npc, StatBlockId = Acolyte },
            Aldous,
            new StatBlock());

        Assert.Contains("Uses a stat block that is not extracted yet", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_still_shows_its_block_after_its_description()
    {
        string html = await Render(
            new ReferenceCard
            {
                NodeId = "bell-warden",
                Label = "The Bell-Warden",
                Kind = CrossRefKind.Monster,
                Summary = "Stands among the nave's statues.",
                StatBlockId = Gargoyle,
            },
            new NpcDossier(),
            new StatBlock { NodeId = Gargoyle, Name = "Gargoyle" });

        int summary = html.IndexOf("Stands among the nave", StringComparison.Ordinal);
        int block = html.IndexOf("Uses the Gargoyle stat block", StringComparison.Ordinal);
        Assert.True(summary >= 0, "the entity's own description is shown");
        Assert.True(block > summary, "the block comes after the description");
    }

    private static async Task<string> Render(ReferenceCard card, NpcDossier npc, StatBlock block)
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [nameof(ReferenceDrawer.Card)] = card,
                [nameof(ReferenceDrawer.Npc)] = npc,
                [nameof(ReferenceDrawer.StatBlock)] = block,
            });

            HtmlRootComponent output = await renderer.RenderComponentAsync<ReferenceDrawer>(parameters);
            return output.ToHtmlString();
        });
    }
}
