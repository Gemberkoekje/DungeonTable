using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Dossier;
using DungeonTable.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Renders the prose component every dossier text goes through, and checks that an authored link
/// reaches the page as its words: a button when it resolves, marked text when it does not, and
/// never the brackets.
/// </summary>
public sealed class CrossRefProseTests
{
    private const string Text = "Four [[bandit|human bandits]] guard [[bandit-captin|the captain]].";

    [Fact]
    public async Task A_resolved_link_renders_its_words_as_a_link_button()
    {
        string html = await Render(Text, Resolve);

        Assert.Contains("class=\"xref xref-monster\"", html, StringComparison.Ordinal);
        Assert.Contains(">human bandits</button>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("[[bandit|", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_broken_link_renders_its_words_marked_with_the_markup_in_its_tooltip()
    {
        string html = await Render(Text, Resolve);

        Assert.Contains(
            "<span class=\"xref-unresolved\" title=\"Broken link: [[bandit-captin|the captain]] names nothing that is loaded\">the captain</span>",
            html,
            StringComparison.Ordinal);
        Assert.Equal(1, html.Split("<button").Length - 1);
    }

    [Fact]
    public async Task A_link_to_a_floor_not_authored_yet_is_muted_text_rather_than_broken()
    {
        string html = await Render(
            "Down to [[L2 area 1|the second level]].",
            _ => new[] { new CrossRef { Text = "the second level", Kind = CrossRefKind.Pending, TargetId = string.Empty, Start = 8, Length = 30 } });

        Assert.Equal(
            "Down to <span class=\"xref-pending\" title=\"Not authored yet: [[L2 area 1|the second level]] is on a floor with no dossier\">the second level</span>.",
            html);
    }

    [Fact]
    public async Task Without_a_resolver_a_link_is_reduced_to_its_words()
    {
        string html = await Render(Text, null);

        Assert.Equal("Four human bandits guard the captain.", html);
    }

    // What the markup resolver returns for the fixture text: one link that resolved, one that did not.
    private static IReadOnlyList<CrossRef> Resolve(string text) => new[]
    {
        new CrossRef { Text = "human bandits", Kind = CrossRefKind.Monster, TargetId = "bandit-node", Start = 5, Length = 24 },
        new CrossRef { Text = "the captain", Kind = CrossRefKind.Unresolved, TargetId = string.Empty, Start = 36, Length = 29 },
    };

    private static async Task<string> Render(string text, Func<string, IReadOnlyList<CrossRef>> resolve)
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [nameof(CrossRefProse.Text)] = text,
                [nameof(CrossRefProse.ResolveRefs)] = resolve,
            });

            HtmlRootComponent output = await renderer.RenderComponentAsync<CrossRefProse>(parameters);
            return output.ToHtmlString();
        });
    }
}
