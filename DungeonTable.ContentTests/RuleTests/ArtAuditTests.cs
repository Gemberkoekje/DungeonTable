using System.Collections.Generic;
using System.IO;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Art;
using DungeonTable.Core.Stats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>
/// Proves the art rules on real pictures: an entry the catalogue drops, a picture the art route would
/// not serve or whose size is stale, a link to no stat block, an entry nobody could find, and a picture
/// in no entry.
/// </summary>
public sealed class ArtAuditTests
{
    private static readonly string[] OnlyTheWarden = { "demo/bell-warden.png" };

    private static readonly string[] TagAndKind = { "'untagged'", "'unkinded'" };

    [Fact]
    public void The_pictures_on_disk_are_the_served_kinds_named_as_a_catalogue_names_them_uploads_aside()
    {
        using var content = new TempContent();
        Picture(content, "data/art/demo/bell-warden.png", 4, 3);
        Picture(content, "data/art/uploads/sketch.png", 4, 3);
        content.Write("data/art/demo/notes.txt", "not a picture");
        content.Write("data/art/demo/sprite.gif", "not served");

        Assert.Equal(OnlyTheWarden, ArtAudit.PicturesOnDisk(ArtFolder(content)));
    }

    [Fact]
    public void An_entry_with_no_id_or_no_file_or_a_shared_id_is_reported()
    {
        var catalogue = new ArtCatalogue
        {
            Images = new[]
            {
                Entry("demo-bell-warden", "demo/bell-warden.png"),
                Entry(string.Empty, "demo/wenna.png"),
                Entry("demo-grask", string.Empty),
                Entry("demo-bell-warden", "demo/bell-warden-2.png"),
            },
        };

        IReadOnlyList<string> faults = ArtAudit.Dropped(catalogue);

        Assert.Equal(3, faults.Count);
        Assert.Contains("images[1]", faults[0], StringComparison.Ordinal);
        Assert.Contains("images[2]", faults[1], StringComparison.Ordinal);
        Assert.Contains("Two pictures have the id 'demo-bell-warden'", faults[2], StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_whose_picture_is_missing_misnamed_or_of_a_kind_the_route_refuses_is_reported()
    {
        var onDisk = new HashSet<string>(StringComparer.Ordinal) { "demo/bell-warden.png", "demo/wenna.webp" };
        var catalogue = new ArtCatalogue
        {
            Images = new[]
            {
                Entry("fine", "demo/bell-warden.png"),
                Entry("missing", "demo/grask.png"),
                Entry("misnamed", "Demo/Wenna.webp"),
                Entry("refused", "demo/sprite.gif"),
            },
        };

        IReadOnlyList<string> faults = ArtAudit.Unserved(catalogue, onDisk);

        Assert.Equal(3, faults.Count);
        Assert.Contains("'missing' is demo/grask.png, and no picture has that name", faults[0], StringComparison.Ordinal);
        Assert.Contains("'misnamed' is Demo/Wenna.webp, and no picture has that name, casing and all", faults[1], StringComparison.Ordinal);
        Assert.Contains("does not serve .gif files", faults[2], StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_whose_size_is_not_its_pictures_or_whose_picture_is_damaged_is_reported()
    {
        using var content = new TempContent();
        Picture(content, "data/art/demo/right.png", 4, 3);
        Picture(content, "data/art/demo/stale.png", 4, 3);
        content.Write("data/art/demo/damaged.png", "not a png at all");
        var catalogue = new ArtCatalogue
        {
            Images = new[]
            {
                Entry("right", "demo/right.png", 4, 3),
                Entry("stale", "demo/stale.png", 5, 3),
                Entry("damaged", "demo/damaged.png", 4, 3),
                Entry("missing", "demo/missing.png", 4, 3),
            },
        };

        IReadOnlyList<string> faults = ArtAudit.WrongSizes(catalogue, ArtFolder(content), ArtAudit.PicturesOnDisk(ArtFolder(content)));

        Assert.Equal(2, faults.Count);
        Assert.Contains("'stale' says 5x3, and demo/stale.png is 4x3", faults[0], StringComparison.Ordinal);
        Assert.Contains("'damaged': demo/damaged.png cannot be read as a picture", faults[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_link_to_no_stat_block_the_library_has_is_reported()
    {
        var stats = new FakeStatLibrary();
        stats.Monsters["data_statblocks_monsters_gargoyle"] = new StatBlock { NodeId = "data_statblocks_monsters_gargoyle", Name = "Gargoyle" };
        ArtImage warden = Entry("demo-bell-warden", "demo/bell-warden.png");
        var catalogue = new ArtCatalogue
        {
            Images = new[]
            {
                new ArtImage { Id = warden.Id, File = warden.File, Title = warden.Title, NodeIds = new[] { "data_statblocks_monsters_gargoyle", "data_statblocks_monsters_gargoil" } },
            },
        };

        string fault = Assert.Single(ArtAudit.UnknownNodes(catalogue, stats));

        Assert.Contains("links 'data_statblocks_monsters_gargoil'", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_with_no_title_subject_kind_or_tag_is_reported()
    {
        var catalogue = new ArtCatalogue
        {
            Images = new[]
            {
                Entry("fine", "demo/a.png"),
                new ArtImage { Id = "untagged", File = "demo/b.png", Title = "B", Subject = "A room", Kind = ArtKind.Location },
                new ArtImage { Id = "unkinded", File = "demo/c.png", Title = "C", Subject = "A room", Tags = new[] { "room" } },
            },
        };

        IReadOnlyList<string> faults = ArtAudit.Thin(catalogue);

        Assert.Equal(TagAndKind, faults.Select(fault => fault[..fault.IndexOf(' ', StringComparison.Ordinal)]));
    }

    [Fact]
    public void A_picture_no_entry_names_is_reported()
    {
        var onDisk = new HashSet<string>(StringComparer.Ordinal) { "demo/bell-warden.png", "demo/forgotten.png" };
        var catalogue = new ArtCatalogue { Images = new[] { Entry("demo-bell-warden", "demo/bell-warden.png") } };

        string fault = Assert.Single(ArtAudit.Orphans(catalogue, onDisk));

        Assert.Contains("demo/forgotten.png is in no catalogue entry", fault, StringComparison.Ordinal);
    }

    private static ArtImage Entry(string id, string file, int width = 4, int height = 3) => new()
    {
        Id = id,
        File = file,
        Title = "The Bell-Warden",
        Subject = "A gargoyle on a plinth",
        Kind = ArtKind.Creature,
        Tags = new[] { "gargoyle" },
        Width = width,
        Height = height,
    };

    private static string ArtFolder(TempContent content) => Path.Combine(content.Root, "data", "art");

    private static void Picture(TempContent content, string relativePath, int width, int height)
    {
        string path = content.Write(relativePath, string.Empty);
        using var image = new Image<Rgba32>(width, height);
        image.SaveAsPng(path);
    }
}
