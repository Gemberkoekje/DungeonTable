using System.IO;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Content;

namespace DungeonTable.Tests.Content;

/// <summary>
/// Which pictures show what, on the sample pack, as the content and the catalogue change while the app
/// runs: a picture's names follow the content they resolve against, and an edit to the catalogue is
/// read the next time anyone asks.
/// </summary>
public sealed class LivePictureLinksTests : IDisposable
{
    private const string Gargoyle = "data_statblocks_monsters_gargoyle";

    private readonly PackCopy pack = new PackCopy();

    public void Dispose() => pack.Dispose();

    [Fact]
    public void The_sample_packs_pictures_are_found_by_who_and_what_they_show()
    {
        (LiveContent live, FileSystemArtCatalogue art) = Open();
        var links = new LivePictureLinks(live, art);

        Assert.Equal("demo-wenna-brask", Assert.Single(links.PicturesOf("wenna-brask")).Id);
        Assert.Equal("demo-bell-warden", Assert.Single(links.PicturesOf("bell-warden")).Id);
        Assert.Equal("demo-bell-warden", Assert.Single(links.PicturesOf(Gargoyle)).Id);
        Assert.Empty(links.PicturesOf("grask"));
    }

    [Fact]
    public void An_edit_to_the_committed_catalogue_is_followed_and_announced()
    {
        (LiveContent live, FileSystemArtCatalogue art) = Open();
        var links = new LivePictureLinks(live, art);
        int raised = 0;
        links.Changed += () => raised++;
        Assert.Empty(links.PicturesOf("grask"));

        Edit("data/art/catalogue.json", "\"shows\": [ \"wenna-brask\" ]", "\"shows\": [ \"grask\", \"wenna-brask\" ]");
        art.ReloadCommitted();

        Assert.Equal("demo-wenna-brask", Assert.Single(links.PicturesOf("grask")).Id);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void A_content_reload_resolves_the_names_again_and_is_announced()
    {
        (LiveContent live, FileSystemArtCatalogue art) = Open();
        var links = new LivePictureLinks(live, art);
        int raised = 0;
        links.Changed += () => raised++;
        Assert.Single(links.PicturesOf(Gargoyle));

        // The stat block is renamed: the Bell-Warden's picture, which shows "gargoyle", no longer shows it.
        Edit("data/statblocks/monsters-srd.json", "\"name\": \"Gargoyle\"", "\"name\": \"Stone Gargoyle\"");
        Assert.True(live.ReloadDocuments().Applied);

        Assert.Empty(links.PicturesOf(Gargoyle));
        Assert.Single(links.PicturesOf("bell-warden"));
        Assert.Equal(1, raised);
    }

    [Fact]
    public void A_map_changing_on_disk_is_not_announced()
    {
        (LiveContent live, FileSystemArtCatalogue art) = Open();
        var links = new LivePictureLinks(live, art);
        int raised = 0;
        links.Changed += () => raised++;

        live.Announce(ContentChanges.Maps);

        Assert.Equal(0, raised);
    }

    private (LiveContent Live, FileSystemArtCatalogue Art) Open() => (
        new LiveContent(
            Path.Combine(pack.Root, "data", "dossiers"),
            Path.Combine(pack.Root, "data", "statblocks"),
            Path.Combine(pack.Root, "data", "book-index")),
        new FileSystemArtCatalogue(Path.Combine(pack.Root, "data", "art")));

    private void Edit(string relative, string from, string to)
    {
        string path = Path.Combine(pack.Root, relative);
        string text = File.ReadAllText(path);
        Assert.Contains(from, text, StringComparison.Ordinal);
        File.WriteAllText(path, text.Replace(from, to, StringComparison.Ordinal));
    }
}
