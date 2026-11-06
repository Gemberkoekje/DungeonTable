using DungeonTable.Core.Art;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The one-click picture behind every mention: it puts the picture up, takes it down when it is the one
/// showing, and tells the buttons only when the picture on show changed, not on every change of the table.
/// </summary>
public sealed class ProjectorPicturesTests
{
    private static readonly ArtImage Wenna = new ArtImage { Id = "demo-wenna-brask", Title = "Wenna Brask" };
    private static readonly ArtImage Warden = new ArtImage { Id = "demo-bell-warden", Title = "The Bell-Warden" };

    [Fact]
    public void A_click_puts_the_picture_up_and_a_second_takes_it_down()
    {
        var session = new SessionState();
        ProjectorPictures pictures = Pictures(session);

        pictures.Toggle(Wenna.Id);

        Assert.Equal(Wenna.Id, session.ShownArtImageId);
        Assert.True(session.ShownArtVisible);
        Assert.True(pictures.IsShowing(Wenna.Id));

        pictures.Toggle(Wenna.Id);

        Assert.False(session.ShownArtVisible);
        Assert.Equal(Wenna.Id, session.ShownArtImageId);
        Assert.False(pictures.IsShowing(Wenna.Id));
    }

    [Fact]
    public void A_click_on_another_picture_replaces_the_one_showing()
    {
        var session = new SessionState();
        ProjectorPictures pictures = Pictures(session);
        pictures.Toggle(Wenna.Id);

        pictures.Toggle(Warden.Id);

        Assert.Equal(Warden.Id, session.ShownArtImageId);
        Assert.True(session.ShownArtVisible);
        Assert.False(pictures.IsShowing(Wenna.Id));
    }

    [Fact]
    public void A_picture_staged_but_hidden_is_not_showing_and_a_click_puts_it_back()
    {
        var session = new SessionState();
        ProjectorPictures pictures = Pictures(session);
        session.ShowArt(Wenna.Id);
        session.HideArt();

        Assert.False(pictures.IsShowing(Wenna.Id));

        pictures.Toggle(Wenna.Id);

        Assert.True(session.ShownArtVisible);
    }

    [Fact]
    public void The_buttons_hear_of_a_picture_going_up_or_down_and_of_nothing_else_on_the_table()
    {
        var session = new SessionState();
        var links = new FakePictureLinks();
        ProjectorPictures pictures = Pictures(session, links);
        int raised = 0;
        pictures.Changed += () => raised++;

        session.SetArtSize(ArtSize.Large);
        session.AddToTray(Warden.Id);
        Assert.Equal(0, raised);

        session.ShowArt(Wenna.Id);
        session.ShowArt(Wenna.Id);
        Assert.Equal(1, raised);

        session.HideArt();
        Assert.Equal(2, raised);

        links.Announce();
        Assert.Equal(3, raised);
    }

    [Fact]
    public void A_thing_with_no_picture_has_none_and_a_blank_click_does_nothing()
    {
        var session = new SessionState();
        ProjectorPictures pictures = Pictures(session);

        pictures.Toggle(string.Empty);

        Assert.False(pictures.Has("grask"));
        Assert.True(pictures.Has("wenna-brask"));
        Assert.False(pictures.Has(string.Empty));
        Assert.Equal(string.Empty, session.ShownArtImageId);
    }

    private static ProjectorPictures Pictures(SessionState session, FakePictureLinks links = null)
    {
        links ??= new FakePictureLinks();
        links.Pictures["wenna-brask"] = new[] { Wenna };
        return new ProjectorPictures(links, session);
    }
}
