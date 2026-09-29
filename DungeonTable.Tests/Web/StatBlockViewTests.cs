using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Stats;
using DungeonTable.Web.Components.Dm;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The full stat block and spell views: each entry reads as the book prints it, its name followed by
/// a space, and the block is cited by its book's title where the caller knows it.
/// </summary>
public sealed class StatBlockViewTests
{
    // An attack whose parse loses its target: the parsed line said "one target".
    private static readonly StatBlockEntry Swarm = new StatBlockEntry
    {
        Name = "Bites",
        Text = "Melee Weapon Attack: +6 to hit, reach 0 ft., one creature in the swarm's space. Hit: 14 (4d6) piercing damage.",
        Attacks = new[] { new AttackLine { Name = "Bites", ToHit = 6, Targets = 1, DamageDice = "4d6", DamageAverage = 14, DamageType = "piercing" } },
    };

    [Fact]
    public async Task An_entrys_name_is_followed_by_a_space_and_its_full_text()
    {
        string html = await Render<StatBlockView>(nameof(StatBlockView.Block), new StatBlock { NodeId = "swarm-of-rats", Name = "Swarm of Rats", Actions = new[] { Swarm } });

        Assert.Contains("<strong>Bites.</strong> Melee Weapon Attack: &#x2B;6 to hit, reach 0 ft., one creature in the swarm&#x27;s space.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("one target", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entry_written_without_text_shows_its_parsed_attack()
    {
        var bite = new StatBlockEntry
        {
            Name = "Bite",
            Attacks = new[] { new AttackLine { ToHit = 4, Reach = 5, Targets = 1, DamageDice = "1d6 + 2", DamageAverage = 5, DamageType = "piercing" } },
        };

        string html = await Render<StatBlockView>(nameof(StatBlockView.Block), new StatBlock { NodeId = "wolf", Name = "Wolf", Actions = new[] { bite } });

        Assert.Contains("<strong>Bite.</strong> &#x2B;4 to hit, reach 5 ft., one target. Hit: 5 (1d6 &#x2B; 2) piercing damage.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_block_is_cited_by_its_books_title_when_given_and_by_its_file_otherwise()
    {
        var block = new StatBlock { NodeId = "bugbear", Name = "Bugbear", Source = "SRD_CC_v5.1.pdf", SourceLocation = "p.266" };

        string titled = await Render<StatBlockView>(nameof(StatBlockView.Block), block, nameof(StatBlockView.Citation), "SRD 5.1 p.266");
        string untitled = await Render<StatBlockView>(nameof(StatBlockView.Block), block);

        Assert.Contains("<p class=\"reference-citation\">SRD 5.1 p.266</p>", titled, StringComparison.Ordinal);
        Assert.DoesNotContain("SRD_CC_v5.1.pdf", titled, StringComparison.Ordinal);
        Assert.Contains("SRD_CC_v5.1.pdf p.266", untitled, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_spell_is_cited_by_its_books_title_when_given()
    {
        var spell = new SpellEntry { NodeId = "sacred-flame", Name = "Sacred Flame", Source = "SRD_CC_v5.1.pdf", SourceLocation = "p.176" };

        string html = await Render<SpellDetail>(nameof(SpellDetail.Spell), spell, nameof(SpellDetail.Citation), "SRD 5.1 p.176");

        Assert.Contains("<p class=\"reference-citation\">SRD 5.1 p.176</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("SRD_CC_v5.1.pdf", html, StringComparison.Ordinal);
    }

    private static async Task<string> Render<TComponent>(string name, object value, string secondName = "", object secondValue = null)
        where TComponent : IComponent
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object> { [name] = value };
            if (secondName.Length > 0)
            {
                parameters[secondName] = secondValue;
            }

            HtmlRootComponent output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
