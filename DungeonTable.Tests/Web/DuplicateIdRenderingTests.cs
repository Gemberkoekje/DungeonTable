using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Dossier;
using DungeonTable.Web.Components.Dm;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// An NPC's, a character's or a scene's id is written by hand, and nothing makes it unique or
/// non-blank. Each was a Blazor <c>@key</c>, and Blazor throws on two siblings with one key, so two NPCs
/// sharing an id took the whole tab down. Every card still renders now, under a key of its own.
/// </summary>
public sealed class DuplicateIdRenderingTests
{
    [Fact]
    public async Task Npcs_with_a_repeated_or_a_blank_id_all_render()
    {
        var roster = new NpcRoster
        {
            Npcs = new[]
            {
                new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask" },
                new NpcDossier { Id = "wenna-brask", Name = "Wenna the Elder" },
                new NpcDossier { Id = string.Empty, Name = "Old Hobb" },
                new NpcDossier { Id = string.Empty, Name = "The Sexton" },
            },
        };

        string html = await Render<NpcPanel>(nameof(NpcPanel.Roster), roster);

        Assert.Contains("Wenna Brask", html, StringComparison.Ordinal);
        Assert.Contains("Wenna the Elder", html, StringComparison.Ordinal);
        Assert.Contains("Old Hobb", html, StringComparison.Ordinal);
        Assert.Contains("The Sexton", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Characters_with_a_repeated_id_all_render()
    {
        var party = new PartyDossier
        {
            Characters = new[]
            {
                new CharacterDossier { Id = "maren", Name = "Maren" },
                new CharacterDossier { Id = "maren", Name = "Maren's Twin" },
            },
        };

        string html = await Render<PartyPanel>(nameof(PartyPanel.Party), party);

        Assert.Contains("Maren&#x27;s Twin", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scenes_with_a_repeated_id_all_render()
    {
        var log = new SessionLog
        {
            Sessions = new[]
            {
                new SessionPlan
                {
                    Id = "session-1",
                    Title = "The Silent Bell",
                    Scenes = new[]
                    {
                        new SessionScene { Id = "scene", Ordinal = 0, Title = "At the Mill" },
                        new SessionScene { Id = "scene", Ordinal = 1, Title = "Into the Undercroft" },
                    },
                },
            },
        };

        string html = await Render<SessionPanel>(nameof(SessionPanel.Log), log);

        Assert.Contains("At the Mill", html, StringComparison.Ordinal);
        Assert.Contains("Into the Undercroft", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_unique_id_is_its_own_key_and_a_repeat_is_told_apart()
    {
        IReadOnlyList<Keyed<string>> keyed = RenderKeys.Of(new[] { "a", "b", "a", string.Empty, string.Empty }, id => id);

        Assert.Equal(("a", 0), keyed[0].Key);
        Assert.Equal(("b", 0), keyed[1].Key);
        Assert.Equal(("a", 1), keyed[2].Key);
        Assert.Equal((string.Empty, 0), keyed[3].Key);
        Assert.Equal((string.Empty, 1), keyed[4].Key);
        Assert.Equal(5, keyed.Select(entry => entry.Key).Distinct().Count());
    }

    // Renders the component, then renders it again: Blazor checks keys only when it diffs a list against
    // the one it rendered before, which a live tab does on its next change.
    private static async Task<string> Render<TComponent>(string parameter, object value)
        where TComponent : IComponent
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        RenderAgainHost host = null;

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment content = builder =>
            {
                builder.OpenComponent<TComponent>(0);
                builder.AddComponentParameter(1, parameter, value);
                builder.CloseComponent();
            };
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [nameof(RenderAgainHost.ChildContent)] = content,
                [nameof(RenderAgainHost.OnReady)] = (Action<RenderAgainHost>)(ready => host = ready),
            });

            HtmlRootComponent output = await renderer.RenderComponentAsync<RenderAgainHost>(parameters);
            await host.RenderAgainAsync();
            return output.ToHtmlString();
        });
    }
}
