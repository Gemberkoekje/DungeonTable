using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Books;
using DungeonTable.Infrastructure.Dossiers;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Infrastructure.Rosters;
using DungeonTable.Infrastructure.Stats;
using DungeonTable.Tests.Maps;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DungeonTable.Tests.Web;

/// <summary>
/// <c>Content:Root</c> points all six content locators at one folder holding the content in its
/// usual layout. A locator's own key still wins over it, and walking up from the start directory is
/// still the last resort, so a checkout where app and content share a folder works as before.
/// </summary>
public sealed class ContentRootTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "dt-content-" + Guid.NewGuid().ToString("N"));

    public ContentRootTests() => Directory.CreateDirectory(tempRoot);

    /// <summary>The six kinds of content the app locates at startup.</summary>
    public enum ContentKind
    {
        None,
        Maps,
        Dossiers,
        Stats,
        Art,
        Rosters,
        BookIndex,
    }

    public static TheoryData<ContentKind> AllKinds => new()
    {
        ContentKind.Maps,
        ContentKind.Dossiers,
        ContentKind.Stats,
        ContentKind.Art,
        ContentKind.Rosters,
        ContentKind.BookIndex,
    };

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void The_content_root_is_preferred_over_the_walk_up(ContentKind kind)
    {
        Create(kind, tempRoot);
        string expected = Create(kind, Path.Combine(tempRoot, "content"));

        string found = Locate(kind, Start(), configuredPath: string.Empty, Path.Combine(tempRoot, "content"));

        Assert.Equal(expected, found);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void A_locators_own_key_is_preferred_over_the_content_root(ContentKind kind)
    {
        Create(kind, Path.Combine(tempRoot, "content"));
        string own = Create(kind, Path.Combine(tempRoot, "own"));

        string found = Locate(kind, Start(), own, Path.Combine(tempRoot, "content"));

        Assert.Equal(own, found);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void The_walk_up_still_finds_what_the_content_root_lacks(ContentKind kind)
    {
        string expected = Create(kind, tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, "content"));

        string found = Locate(kind, Start(), configuredPath: string.Empty, Path.Combine(tempRoot, "content"));

        Assert.Equal(expected, found);
    }

    [Fact]
    public void A_relative_content_root_is_relative_to_the_start_directory()
    {
        string expected = Create(ContentKind.Dossiers, Path.Combine(tempRoot, "content"));

        string found = DossierFileLocator.Locate(Start(), configuredPath: string.Empty, Path.Combine("..", "..", "content"));

        Assert.Equal(expected, found);
    }

    [Fact]
    public void A_missing_book_index_is_none_rather_than_an_error()
    {
        // Every other kind of content is required. A campaign without a book index still runs, with
        // search reaching only its own content.
        Directory.CreateDirectory(Path.Combine(tempRoot, "content"));

        Assert.Equal(string.Empty, BookIndexFileLocator.Locate(Start(), configuredPath: string.Empty, Path.Combine(tempRoot, "content")));
    }

    [Fact]
    public void Maps_under_the_content_root_need_no_map_in_them_yet()
    {
        // The walk-up skips a Maps folder with no map in it, since it may be an unrelated folder of
        // that name. A content root is chosen on purpose, and a new campaign has no map yet.
        string maps = Path.Combine(tempRoot, "content", "Maps");
        Directory.CreateDirectory(maps);

        Assert.Equal(maps, MapFileLocator.Locate(Start(), configuredPath: string.Empty, Path.Combine(tempRoot, "content")));
    }

    [Fact]
    public async Task The_app_finds_all_of_its_content_through_the_content_root_alone()
    {
        // ASP.NET's own content root is an empty directory, so walking up from it finds nothing.
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(Start());
            builder.UseSetting("Content:Root", SamplePack.Root);
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);
        });
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{MapServingFixture.Passphrase}")));

        HttpResponseMessage response = await client.GetAsync("/dm", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string Locate(ContentKind kind, string start, string configuredPath, string contentRoot) => kind switch
    {
        ContentKind.Maps => MapFileLocator.Locate(start, configuredPath, contentRoot),
        ContentKind.Dossiers => DossierFileLocator.Locate(start, configuredPath, contentRoot),
        ContentKind.Stats => StatFileLocator.Locate(start, configuredPath, contentRoot),
        ContentKind.Art => ArtFileLocator.Locate(start, configuredPath, contentRoot),
        ContentKind.Rosters => RosterFileLocator.Locate(start, configuredPath, contentRoot),
        ContentKind.BookIndex => BookIndexFileLocator.Locate(start, configuredPath, contentRoot),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of content."),
    };

    // Lays out one kind of content under root the way a content folder holds it, and returns the
    // path its locator should come back with.
    private static string Create(ContentKind kind, string root)
    {
        switch (kind)
        {
            case ContentKind.Maps:
                // The walk-up only accepts a Maps folder with a map in it.
                string maps = Path.Combine(root, "Maps");
                Directory.CreateDirectory(maps);
                File.WriteAllText(Path.Combine(maps, "level.regions.json"), "{}");
                return maps;

            default:
                string directory = kind switch
                {
                    ContentKind.Dossiers => Path.Combine(root, "data", "dossiers"),
                    ContentKind.Stats => Path.Combine(root, "data", "statblocks"),
                    ContentKind.Art => Path.Combine(root, "data", "art"),
                    ContentKind.Rosters => Path.Combine(root, "data"),
                    ContentKind.BookIndex => Path.Combine(root, "data", "book-index"),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of content."),
                };
                Directory.CreateDirectory(directory);
                return directory;
        }
    }

    // Two levels below the temp root, so the walk-up has somewhere to walk to.
    private string Start()
    {
        string start = Path.Combine(tempRoot, "app", "bin");
        Directory.CreateDirectory(start);
        return start;
    }
}
