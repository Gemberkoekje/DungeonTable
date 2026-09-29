using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Art;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Content;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace DungeonTable.Tests.Art;

/// <summary>
/// Verifies the file-system art catalogue reads the committed and uploaded documents, indexes both
/// by id and by the node ids they depict, and — the half that matters — refuses, re-encodes and cleans
/// up after uploads. Uses a throwaway temp directory as the art root, mirroring
/// <c>FileSystemStatLibraryTests</c>.
/// </summary>
public sealed class FileSystemArtCatalogueTests : IDisposable
{
    // Hoisted out of the assertion itself (CA1861), as elsewhere in this suite.
    private static readonly string[] RankedBest = { "exact-title", "by-prefix", "by-tag", "by-subject" };

    private readonly string tempRoot =
        Path.Combine(Path.GetTempPath(), "dt-art-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Loads_the_committed_catalogue_and_indexes_it()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        var image = catalogue.GetImage("mm-vampire-spawn");
        Assert.True(image.IsValid);
        Assert.Equal("Vampire spawn", image.Value.Title);
        Assert.Equal(ArtKind.Creature, image.Value.Kind);
        Assert.Equal(ArtOrigin.Book, image.Value.Origin);
        Assert.Equal(803, image.Value.Width);

        Assert.Equal(2, catalogue.All().Count);
        Assert.Equal(
            "mm-vampire-spawn",
            Assert.Single(catalogue.ForNode("data_statblocks_monsters_vampire")).Id);
        Assert.Empty(catalogue.ForNode("data_statblocks_monsters_nothing"));
    }

    [Fact]
    public void Reading_the_committed_catalogue_again_takes_an_edit_and_keeps_the_uploads()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);
        Assert.True(catalogue.Upload("The mill", Png(40, 30)).IsValid);
        int changed = 0;
        catalogue.Changed += () => changed++;

        File.WriteAllText(Path.Combine(tempRoot, "catalogue.json"), """
            { "images": [ { "id": "mm-goblin", "file": "mm/goblin.webp", "title": "Goblin archer", "kind": "creature" } ] }
            """);
        IReadOnlyList<ArtImage> before = catalogue.All();
        Assert.Empty(catalogue.ReloadCommitted());

        Assert.Equal("Goblin archer", catalogue.GetImage("mm-goblin").Value.Title);
        Assert.False(catalogue.GetImage("mm-vampire-spawn").IsValid);
        Assert.Contains(catalogue.All(), image => image.Origin == ArtOrigin.Upload);
        Assert.Equal(before.Count - 1, catalogue.All().Count);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void A_committed_catalogue_that_stops_parsing_keeps_the_pictures_it_listed()
    {
        // A half-saved file must not empty the picker in the middle of a session.
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);
        int changed = 0;
        catalogue.Changed += () => changed++;

        File.WriteAllText(Path.Combine(tempRoot, "catalogue.json"), "{ \"images\": [ ");
        IReadOnlyList<ContentProblem> found = catalogue.ReloadCommitted();

        Assert.True(Assert.Single(found).DocumentSkipped);
        Assert.Equal(2, catalogue.All().Count);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void A_committed_entry_that_states_no_origin_is_committed_art()
    {
        // The catalogue a picture is read from decides its origin, so a committed document need not
        // say it. A campaign's own paintings come from no book, and "book" would misname them.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "catalogue.json"), """
            { "images": [ { "id": "demo-bell-warden", "file": "demo/bell-warden.webp", "title": "The Bell-Warden", "kind": "creature" } ] }
            """);

        var catalogue = new FileSystemArtCatalogue(tempRoot);

        Assert.Equal(ArtOrigin.Book, catalogue.GetImage("demo-bell-warden").Value.Origin);
        Assert.False(catalogue.Remove("demo-bell-warden").IsValid);
    }

    [Fact]
    public void A_missing_or_corrupt_catalogue_leaves_an_empty_store_rather_than_throwing()
    {
        Directory.CreateDirectory(tempRoot);
        Assert.Empty(new FileSystemArtCatalogue(tempRoot).All());

        File.WriteAllText(Path.Combine(tempRoot, "catalogue.json"), "{ not json");
        Assert.Empty(new FileSystemArtCatalogue(tempRoot).All());
    }

    [Fact]
    public void A_null_list_or_a_null_entry_in_the_catalogue_is_read_leniently()
    {
        // The loader already skipped a null picture, but a null tag reached search, which reads every
        // tag: one null broke the Art tab's picker for any query that got that far.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "catalogue.json"), """
            {
              "images": [
                null,
                { "id": "demo-bell-warden", "file": "demo/bell-warden.webp", "title": "The Bell-Warden",
                  "kind": "creature", "tags": [ null, "gargoyle" ], "nodeIds": null }
              ]
            }
            """);

        var catalogue = new FileSystemArtCatalogue(tempRoot);

        ArtImage image = Assert.Single(catalogue.All());
        Assert.Equal("gargoyle", Assert.Single(image.Tags));
        Assert.Empty(image.NodeIds);
        Assert.Equal("demo-bell-warden", Assert.Single(catalogue.Search("gargoyle", 10)).Id);
    }

    [Fact]
    public void Search_matches_a_title_a_tag_and_a_subject()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        Assert.Equal("mm-bugbear", catalogue.Search("Bugbear", 10)[0].Id);

        // "undead" is only a tag, and only on one of the two.
        Assert.Equal("mm-vampire-spawn", Assert.Single(catalogue.Search("undead", 10)).Id);

        // "morningstar" appears only in the bugbear's subject line.
        Assert.Equal("mm-bugbear", Assert.Single(catalogue.Search("morningstar", 10)).Id);

        Assert.Empty(catalogue.Search("   ", 10));
        Assert.Empty(catalogue.Search("bugbear", 0));
    }

    [Fact]
    public void Search_ranks_an_exact_title_over_a_prefix_over_a_tag_over_a_subject()
    {
        // One needle that hits FOUR entries at four different rank levels, because a ranking test
        // whose query only ever matches one candidate never compares anything: the losers are
        // filtered out before the sort, so reversing the order or permuting the levels would leave
        // every assertion passing.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "catalogue.json"), """
            {
              "images": [
                { "id": "by-subject", "file": "mm/d.webp", "title": "Zzz", "kind": "creature",
                  "subject": "A wisp of shadow", "tags": [ "undead" ] },
                { "id": "by-tag", "file": "mm/c.webp", "title": "Yyy", "kind": "creature",
                  "subject": "Nothing here", "tags": [ "shadow" ] },
                { "id": "by-prefix", "file": "mm/b.webp", "title": "Shadow demon", "kind": "creature",
                  "subject": "Nothing here", "tags": [ "fiend" ] },
                { "id": "exact-title", "file": "mm/a.webp", "title": "Shadow", "kind": "creature",
                  "subject": "Nothing here", "tags": [ "fiend" ] }
              ]
            }
            """);

        var ranked = new FileSystemArtCatalogue(tempRoot).Search("shadow", 10);

        Assert.Equal(RankedBest, ranked.Select(image => image.Id));
    }

    [Fact]
    public void Search_honours_its_limit()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        // Both fixture images carry the "Bestiary.pdf" source and a shared word in subject.
        Assert.Single(catalogue.Search("a", 1));
    }

    [Fact]
    public void An_upload_is_re_encoded_to_webp_and_downscaled()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        var added = catalogue.Upload("The innkeeper", Png(width: 2400, height: 1200));

        Assert.True(added.IsValid);
        Assert.Equal(ArtOrigin.Upload, added.Value.Origin);
        Assert.Equal("The innkeeper", added.Value.Title);
        Assert.Equal("uploads/upload-the-innkeeper.webp", added.Value.File);
        Assert.Equal(ArtUploadRules.MaxEdgePixels, added.Value.Width);
        Assert.Equal(ArtUploadRules.MaxEdgePixels / 2, added.Value.Height);

        // Stored as WebP whatever came in, so the /art route's allowlist and the file agree.
        string stored = Path.Combine(tempRoot, "uploads", "upload-the-innkeeper.webp");
        Assert.True(File.Exists(stored));
        Assert.Equal("webp", Image.DetectFormat(stored).Name.ToLowerInvariant());

        // And it is immediately visible, without a restart.
        Assert.True(catalogue.GetImage(added.Value.Id).IsValid);
        Assert.Equal(3, catalogue.All().Count);
    }

    [Fact]
    public void An_upload_survives_a_restart_because_its_own_catalogue_was_written()
    {
        WriteBookCatalogue();
        new FileSystemArtCatalogue(tempRoot).Upload("Innkeeper", Png(40, 40));

        var reopened = new FileSystemArtCatalogue(tempRoot);
        var image = reopened.GetImage("upload-innkeeper");

        Assert.True(image.IsValid);
        Assert.Equal(ArtOrigin.Upload, image.Value.Origin);
    }

    [Fact]
    public void An_svg_is_refused_however_it_is_labelled()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        // The exact shape the /art route must never serve: same-origin markup that runs script.
        byte[] svg = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        var refused = catalogue.Upload("map.png", svg);

        Assert.False(refused.IsValid);
        Assert.False(Directory.Exists(Path.Combine(tempRoot, "uploads")));
    }

    [Fact]
    public void A_file_with_a_png_header_that_is_not_an_image_is_refused_by_the_decoder()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        // Passes the magic-byte sniff and nothing else — which is the point of decoding as well as
        // sniffing. Nothing must reach disk.
        var header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var bytes = new List<byte>(header);
        bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(new string('x', 512)));

        var refused = catalogue.Upload("Sneaky", bytes.ToArray());

        Assert.False(refused.IsValid);
        Assert.False(File.Exists(Path.Combine(tempRoot, "uploads", "upload-sneaky.webp")));
    }

    [Fact]
    public void An_empty_or_oversized_upload_is_refused()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        Assert.False(catalogue.Upload("Nothing", Array.Empty<byte>()).IsValid);
        Assert.False(catalogue.Upload("Nothing", null).IsValid);

        var huge = new byte[ArtUploadRules.MaxBytes + 1];
        huge[0] = 0x89;
        huge[1] = 0x50;
        huge[2] = 0x4E;
        huge[3] = 0x47;
        Assert.False(catalogue.Upload("Huge", huge).IsValid);
    }

    [Fact]
    public void A_title_that_collides_gets_its_own_id_rather_than_overwriting()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        string first = catalogue.Upload("Innkeeper", Png(40, 40)).Value.Id;
        string second = catalogue.Upload("Innkeeper", Png(40, 40)).Value.Id;

        Assert.Equal("upload-innkeeper", first);
        Assert.Equal("upload-innkeeper-2", second);
        Assert.Equal(4, catalogue.All().Count);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32")]
    [InlineData("///")]
    [InlineData("....")]
    public void A_title_can_never_steer_the_stored_file_out_of_the_uploads_directory(string title)
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        var added = catalogue.Upload(title, Png(40, 40));

        Assert.True(added.IsValid);
        Assert.StartsWith("uploads/upload-", added.Value.File, StringComparison.Ordinal);
        Assert.DoesNotContain("..", added.Value.File, StringComparison.Ordinal);

        string full = Path.GetFullPath(Path.Combine(tempRoot, added.Value.File));
        Assert.StartsWith(
            Path.GetFullPath(Path.Combine(tempRoot, "uploads")), full, StringComparison.Ordinal);
    }

    [Fact]
    public void Remove_deletes_an_upload_and_refuses_committed_book_art()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);
        string id = catalogue.Upload("Innkeeper", Png(40, 40)).Value.Id;
        string stored = Path.Combine(tempRoot, "uploads", "upload-innkeeper.webp");

        Assert.True(catalogue.Remove(id).IsValid);
        Assert.False(File.Exists(stored));
        Assert.False(catalogue.GetImage(id).IsValid);

        // Book art is under version control and reaches every environment through the bind mount:
        // the app is not allowed to delete it.
        Assert.False(catalogue.Remove("mm-vampire-spawn").IsValid);
        Assert.True(catalogue.GetImage("mm-vampire-spawn").IsValid);
        Assert.False(catalogue.Remove("no-such-id").IsValid);
    }

    [Fact]
    public void An_image_declaring_more_pixels_than_the_limit_is_refused_before_it_is_decoded()
    {
        // The byte cap bounds the download, not the decode: a near-solid-colour PNG declaring
        // enormous dimensions compresses to almost nothing and still asks for gigabytes of pixel
        // buffer. This one is ~72 MP in about 100 KB — comfortably inside MaxBytes, well past
        // MaxPixels — and must be turned away on its header alone.
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        byte[] oversized = Png(12_000, 6_000);
        Assert.True(oversized.Length < ArtUploadRules.MaxBytes, "fixture must be under the byte cap");

        var refused = catalogue.Upload("Enormous", oversized);

        Assert.False(refused.IsValid);
        Assert.Contains("megapixels", refused.Messages[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(tempRoot, "uploads")));
    }

    [Fact]
    public void An_upload_rolls_back_completely_when_the_catalogue_cannot_be_saved()
    {
        // The bytes reach disk before the catalogue does. If the catalogue write then fails, the
        // picture would survive a restart as an orphaned file nothing lists — so Upload has to undo
        // itself. Forced here by putting a *directory* where the catalogue document belongs, which
        // makes the final File.Move fail on every platform.
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, "uploads", "catalogue.json"));

        var refused = catalogue.Upload("Innkeeper", Png(40, 40));

        Assert.False(refused.IsValid);
        Assert.False(File.Exists(Path.Combine(tempRoot, "uploads", "upload-innkeeper.webp")));
        Assert.False(catalogue.GetImage("upload-innkeeper").IsValid);
        Assert.Equal(2, catalogue.All().Count);
    }

    [Fact]
    public void A_removal_that_cannot_be_saved_puts_the_picture_back()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);
        string id = catalogue.Upload("Innkeeper", Png(40, 40)).Value.Id;
        string stored = Path.Combine(tempRoot, "uploads", "upload-innkeeper.webp");

        // Same trick, after a successful upload: replace the catalogue document with a directory.
        string document = Path.Combine(tempRoot, "uploads", "catalogue.json");
        File.Delete(document);
        Directory.CreateDirectory(document);

        Assert.False(catalogue.Remove(id).IsValid);

        // Still listed and still on disk: a failed save must not half-delete the picture.
        Assert.True(catalogue.GetImage(id).IsValid);
        Assert.True(File.Exists(stored));
    }

    [Fact]
    public void The_upload_cap_is_enforced()
    {
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);

        for (int index = 0; index < ArtUploadRules.MaxUploads; index++)
        {
            Assert.True(catalogue.Upload($"Handout {index}", Png(8, 8)).IsValid);
        }

        var refused = catalogue.Upload("One too many", Png(8, 8));

        Assert.False(refused.IsValid);
        Assert.Equal(
            ArtUploadRules.MaxUploads,
            catalogue.All().Count(image => image.Origin == ArtOrigin.Upload));

        // ...and removing one frees exactly one slot again.
        Assert.True(catalogue.Remove("upload-handout-0").IsValid);
        Assert.True(catalogue.Upload("Room again", Png(8, 8)).IsValid);
    }

    [Fact]
    public async Task Concurrent_uploads_never_collide_on_an_id_or_tear_a_reader()
    {
        // The class documents itself as safe for a DM uploading while another circuit renders the
        // picker, and the encode deliberately runs outside the lock — which is exactly the window
        // where two uploads could pick the same id. Same title on every task, so every one of them
        // wants the same slug.
        WriteBookCatalogue();
        var catalogue = new FileSystemArtCatalogue(tempRoot);
        const int Writers = 12;

        var readerFailures = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var readersStop = new CancellationTokenSource();
        Task reader = Task.Run(
            () =>
        {
            while (!readersStop.IsCancellationRequested)
            {
                // Enumerating a torn snapshot would throw or yield a null entry.
                foreach (ArtImage image in catalogue.All())
                {
                    if (image is null || image.Id.Length == 0)
                    {
                        readerFailures.Add("null or empty entry in All()");
                    }
                }

                catalogue.Search("handout", 10);
            }
        },
            TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, Writers)
            .Select(_ => Task.Run(
                () => catalogue.Upload("Handout", Png(8, 8)),
                TestContext.Current.CancellationToken)));

        await readersStop.CancelAsync();
        await reader.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Empty(readerFailures);
        Assert.All(results, result => Assert.True(result.IsValid));

        string[] ids = results.Select(result => result.Value.Id).ToArray();
        Assert.Equal(Writers, ids.Distinct(StringComparer.Ordinal).Count());

        // Every id resolves, and every one has its own file: a collision would have overwritten one.
        Assert.All(ids, id => Assert.True(catalogue.GetImage(id).IsValid));
        Assert.Equal(
            Writers,
            Directory.GetFiles(Path.Combine(tempRoot, "uploads"), "*.webp").Length);
    }

    [Fact]
    public void An_uploads_document_claiming_to_be_book_art_is_still_treated_as_an_upload()
    {
        // A hand-edited uploads/catalogue.json must not be able to make itself undeletable — or,
        // read the other way, to pass itself off as content this repo vouches for.
        WriteBookCatalogue();
        Directory.CreateDirectory(Path.Combine(tempRoot, "uploads"));
        File.WriteAllText(Path.Combine(tempRoot, "uploads", "catalogue.json"), """
            { "images": [ { "id": "sneaky", "file": "uploads/sneaky.webp", "title": "Sneaky",
                            "origin": "book", "kind": "creature" } ] }
            """);

        var catalogue = new FileSystemArtCatalogue(tempRoot);

        Assert.Equal(ArtOrigin.Upload, catalogue.GetImage("sneaky").Value.Origin);
        Assert.True(catalogue.Remove("sneaky").IsValid);
    }

    // ---- Fixtures ----------------------------------------------------------------------------

    private static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();
        image.Save(buffer, new PngEncoder());
        return buffer.ToArray();
    }

    private void WriteBookCatalogue()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "catalogue.json"), """
            {
              "images": [
                {
                  "id": "mm-vampire-spawn",
                  "file": "mm/vampire-spawn.webp",
                  "title": "Vampire spawn",
                  "subject": "A red-eyed vampire spawn, fangs bared, mid-lunge",
                  "kind": "creature",
                  "origin": "book",
                  "source": "Bestiary.pdf",
                  "sourceLocation": "p.298",
                  "tags": [ "undead", "vampire" ],
                  "nodeIds": [ "data_statblocks_monsters_vampire" ],
                  "width": 803,
                  "height": 1147
                },
                {
                  "id": "mm-bugbear",
                  "file": "mm/bugbear.webp",
                  "title": "Bugbear",
                  "subject": "A hulking goblinoid shouldering a spiked morningstar",
                  "kind": "creature",
                  "origin": "book",
                  "source": "Bestiary.pdf",
                  "sourceLocation": "p.33",
                  "tags": [ "humanoid", "goblinoid" ],
                  "nodeIds": [ "data_statblocks_monsters_bugbear" ],
                  "width": 900,
                  "height": 1200
                }
              ]
            }
            """);
    }
}
