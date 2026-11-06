using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Art;
using DungeonTable.Core.Dossier;
using DungeonTable.Web.Components.Dm;
using DungeonTable.Web.Components.Shared;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Renders the picture button beside a mention: after a link to something with a picture and nowhere
/// else, lit while that picture is up and lighting up by itself when it goes up, on a card once by the
/// name and, where there are several pictures, once per picture by its title; and nowhere outside the
/// DM screen.
/// </summary>
public sealed class PictureButtonTests
{
    private const string Text = "Ask [[wenna-brask|the miller]] before [[grask]] does.";

    private static readonly ArtImage Wenna = new ArtImage { Id = "demo-wenna-brask", Title = "Wenna Brask" };
    private static readonly ArtImage Warden = new ArtImage { Id = "demo-bell-warden", Title = "The Bell-Warden" };
    private static readonly ArtImage Nave = new ArtImage { Id = "demo-nave", Title = "The lower nave" };

    [Fact]
    public async Task A_link_to_something_with_a_picture_is_followed_by_a_button_that_shows_it()
    {
        var session = new SessionState();

        string html = await RenderOnTheDmScreen<CrossRefProse>(Pictures(session), Prose());

        Assert.Contains(
            ">the miller</button><button type=\"button\" class=\"picture-btn\" aria-pressed=\"false\" title=\"Show &#x201C;Wenna Brask&#x201D; on the projector\"",
            html,
            StringComparison.Ordinal);
        Assert.Equal(1, Count(html, "class=\"picture-btn"));
        Assert.EndsWith(">Grask</button> does.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_button_is_lit_while_its_picture_is_on_the_projector()
    {
        var session = new SessionState();
        session.ShowArt(Wenna.Id);

        string html = await RenderOnTheDmScreen<CrossRefProse>(Pictures(session), Prose());

        Assert.Contains(
            "class=\"picture-btn on\" aria-pressed=\"true\" title=\"Take &#x201C;Wenna Brask&#x201D; off the projector\"",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_button_lights_up_by_itself_when_its_picture_goes_up_and_out_when_it_comes_down()
    {
        var session = new SessionState();
        ProjectorPictures pictures = Pictures(session);
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        HtmlRootComponent root = await renderer.Dispatcher.InvokeAsync(
            () => renderer.RenderComponentAsync<CascadingValue<ProjectorPictures>>(OnTheDmScreen<PictureButton>(pictures, Button("wenna-brask"))));

        session.ShowArt(Wenna.Id);
        string up = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
        session.HideArt();
        string down = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());

        Assert.Contains("class=\"picture-btn on\"", up, StringComparison.Ordinal);
        Assert.Contains("class=\"picture-btn\"", down, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Outside_the_dm_screen_no_button_shows()
    {
        string html = await Render<PictureButton>(Button("wenna-brask"));

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public async Task Something_with_no_picture_gets_no_button()
    {
        string html = await RenderOnTheDmScreen<PictureButton>(Pictures(new SessionState()), Button("grask"));

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public async Task A_card_with_one_picture_shows_it_by_the_name_and_lists_nothing()
    {
        var card = new ReferenceCard { NodeId = "wenna-brask", Label = "Wenna Brask", Kind = CrossRefKind.Book };

        string html = await RenderOnTheDmScreen<ReferenceDrawer>(Pictures(new SessionState()), Drawer(card));

        Assert.Equal(1, Count(html, "class=\"picture-btn"));
        Assert.DoesNotContain("reference-pictures", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_card_with_several_pictures_lists_each_by_its_title_after_the_one_by_the_name()
    {
        var card = new ReferenceCard { NodeId = "bell-warden", Label = "the Bell-Warden", Kind = CrossRefKind.Monster };

        string html = await RenderOnTheDmScreen<ReferenceDrawer>(Pictures(new SessionState()), Drawer(card));

        Assert.Equal(3, Count(html, "class=\"picture-btn"));
        int list = html.IndexOf("reference-pictures", StringComparison.Ordinal);
        int warden = html.IndexOf("<span class=\"picture-title\">The Bell-Warden</span>", StringComparison.Ordinal);
        int nave = html.IndexOf("<span class=\"picture-title\">The lower nave</span>", StringComparison.Ordinal);
        Assert.True(list >= 0 && warden > list && nave > warden, "each picture by its title, best first");
    }

    [Fact]
    public async Task A_persons_card_carries_their_picture_in_its_own_strip_and_not_again_by_the_title()
    {
        var wenna = new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask", Summary = "The miller." };
        var card = new ReferenceCard { NodeId = wenna.Id, Label = wenna.Name, Kind = CrossRefKind.Npc };
        Dictionary<string, object> parameters = Drawer(card);
        parameters[nameof(ReferenceDrawer.Npc)] = wenna;

        string html = await RenderOnTheDmScreen<ReferenceDrawer>(Pictures(new SessionState()), parameters);

        Assert.Equal(1, Count(html, "class=\"picture-btn"));
        int strip = html.IndexOf("character-head", StringComparison.Ordinal);
        Assert.True(strip >= 0 && html.IndexOf("picture-btn", StringComparison.Ordinal) > strip, "the button is in the NPC card's strip");
    }

    private static ProjectorPictures Pictures(SessionState session)
    {
        var links = new FakePictureLinks();
        links.Pictures["wenna-brask"] = new[] { Wenna };
        links.Pictures["bell-warden"] = new[] { Warden, Nave };
        return new ProjectorPictures(links, session);
    }

    private static Dictionary<string, object> Prose() => new Dictionary<string, object>
    {
        [nameof(CrossRefProse.Text)] = Text,
        [nameof(CrossRefProse.ResolveRefs)] = (Func<string, IReadOnlyList<CrossRef>>)(_ => new[]
        {
            new CrossRef { Text = "the miller", Kind = CrossRefKind.Npc, TargetId = "wenna-brask", Start = 4, Length = 26 },
            new CrossRef { Text = "Grask", Kind = CrossRefKind.Monster, TargetId = "grask", Start = 38, Length = 9 },
        }),
    };

    private static Dictionary<string, object> Button(string targetId) => new Dictionary<string, object>
    {
        [nameof(PictureButton.TargetId)] = targetId,
    };

    private static Dictionary<string, object> Drawer(ReferenceCard card) => new Dictionary<string, object>
    {
        [nameof(ReferenceDrawer.Card)] = card,
    };

    private static int Count(string html, string what) => html.Split(what).Length - 1;

    // The component inside the cascade the DM screen puts around everything (DmView).
    private static ParameterView OnTheDmScreen<TComponent>(ProjectorPictures pictures, Dictionary<string, object> parameters)
        where TComponent : IComponent =>
        ParameterView.FromDictionary(new Dictionary<string, object>
        {
            [nameof(CascadingValue<ProjectorPictures>.Value)] = pictures,
            [nameof(CascadingValue<ProjectorPictures>.IsFixed)] = true,
            [nameof(CascadingValue<ProjectorPictures>.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TComponent>(0);
                builder.AddMultipleAttributes(1, parameters);
                builder.CloseComponent();
            }),
        });

    private static async Task<string> RenderOnTheDmScreen<TComponent>(ProjectorPictures pictures, Dictionary<string, object> parameters)
        where TComponent : IComponent
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<CascadingValue<ProjectorPictures>>(OnTheDmScreen<TComponent>(pictures, parameters));
            return output.ToHtmlString();
        });
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
