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
/// An enemy's line on its initiative row: its saving throws and its attacks, so the DM can run it
/// from the order while another creature's pane stays open, and nothing of the rest of its block.
/// </summary>
public sealed class EnemyGlanceTests
{
    // A bugbear-like brute: proficient in Dex saves only, one melee and one thrown attack.
    private static readonly StatBlock Brute = new StatBlock
    {
        NodeId = "brute",
        Name = "Brute",
        ArmourClass = 16,
        Abilities = new AbilityScores { Str = 15, Dex = 14, Con = 13, Intelligence = 8, Wis = 11, Cha = 9 },
        SavingThrows = new[] { new SaveBonus { Ability = "Dex", Bonus = 4 } },
        DamageResistances = new[] { "poison" },
        Traits = new[] { new StatBlockEntry { Name = "Brute", Text = "A melee weapon deals one extra die of its damage." } },
        Actions = new[]
        {
            new StatBlockEntry { Name = "Multiattack", Text = "The brute makes two attacks." },
            new StatBlockEntry
            {
                Name = "Morningstar",
                Text = "Melee Weapon Attack: +4 to hit, reach 5 ft., one target. Hit: 11 (2d8 + 2) piercing damage.",
                Attacks = new[] { new AttackLine { ToHit = 4, Reach = 5, DamageDice = "2d8 + 2", DamageAverage = 11, DamageType = "piercing" } },
            },
            new StatBlockEntry
            {
                Name = "Javelin",
                Text = "Melee or Ranged Weapon Attack: +4 to hit, reach 5 ft. or range 30/120 ft., one target.",
                Attacks = new[]
                {
                    new AttackLine
                    {
                        Name = "Javelin", ToHit = 4, Reach = 5, RangeNormal = 30, RangeLong = 120, DamageDice = "1d6 + 2", DamageAverage = 5, DamageType = "piercing",
                        ExtraDamage = new[] { new DamageRider { DamageDice = "1d6", DamageAverage = 3, DamageType = "poison" } },
                    },
                },
            },
        },
    };

    [Fact]
    public async Task An_enemy_shows_its_six_saves_with_the_proficient_ones_marked()
    {
        string html = await Render(Brute);

        Assert.Contains("<span class=\"glance-save \">Str &#x2B;2</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"glance-save proficient\">Dex &#x2B;4</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"glance-save \">Int -1</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"glance-label\">Saves</span>", html, StringComparison.Ordinal);
        Assert.Equal(6, html.Split("class=\"glance-save ").Length - 1);
    }

    [Fact]
    public async Task An_enemy_shows_its_attacks_with_their_bonus_and_damage_and_its_multiattack()
    {
        string html = await Render(Brute);

        Assert.Contains("The brute makes two attacks.", html, StringComparison.Ordinal);
        Assert.Contains(
            "<span class=\"attack-name\">Morningstar</span>\n                <span class=\"attack-hit\">&#x2B;4</span><span class=\"attack-damage\">11 (2d8 &#x2B; 2) piercing</span>",
            html,
            StringComparison.Ordinal);
        Assert.Contains("5 (1d6 &#x2B; 2) piercing &#x2B; 3 (1d6) poison", html, StringComparison.Ordinal);

        // How far it reaches is in the tooltip, with the book's sentence.
        Assert.Contains("title=\"reach 5 ft. or range 30/120 ft. &#x2014; Melee or Ranged Weapon Attack", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_rest_of_the_block_stays_in_the_pane()
    {
        string html = await Render(Brute);

        Assert.DoesNotContain("poison\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("extra die", html, StringComparison.Ordinal);
        Assert.DoesNotContain("AC 16", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_enemy_with_no_extracted_block_shows_nothing()
    {
        string html = await Render(new StatBlock());

        Assert.Equal(string.Empty, html.Trim());
    }

    private static async Task<string> Render(StatBlock block)
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<EnemyGlance>(
                ParameterView.FromDictionary(new Dictionary<string, object> { [nameof(EnemyGlance.Block)] = block }));
            return output.ToHtmlString();
        });
    }
}
