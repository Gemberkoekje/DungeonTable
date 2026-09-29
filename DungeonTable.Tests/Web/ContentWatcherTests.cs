using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Content;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Infrastructure.Rosters;
using DungeonTable.Web.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The watcher reads the content again when a file in it changes on disk, so an edit made by hand or by
/// an LLM reaches the table without a restart: the documents, a map, the art catalogue.
/// </summary>
public sealed class ContentWatcherTests : IDisposable
{
    // How long a change may take to be noticed and read: file events arrive in milliseconds, the
    // watcher waits a moment for the folder to settle, and the reading itself is quick.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private readonly PackCopy pack = new PackCopy();
    private readonly RecordingLogger<ContentReport> log = new RecordingLogger<ContentReport>();
    private readonly LiveContent live;
    private readonly FileSystemArtCatalogue art;
    private readonly ContentWatcher watcher;

    public ContentWatcherTests()
    {
        string data = Path.Combine(pack.Root, "data");
        string maps = Path.Combine(pack.Root, "Maps");
        live = new LiveContent(Path.Combine(data, "dossiers"), Path.Combine(data, "statblocks"), Path.Combine(data, "book-index"));
        art = new FileSystemArtCatalogue(Path.Combine(data, "art"));
        var report = new ContentReport(
            live,
            art,
            new FileSystemMapStore(maps),
            new FileSystemVectorMapStore(maps, new DungeonScrawlMapReader()),
            new FileSystemPartyRoster(data),
            new FileSystemAllyRoster(data),
            pack.Root,
            log);
        watcher = new ContentWatcher(
            live,
            art,
            report,
            ContentWatcher.Folders(Path.Combine(data, "dossiers"), Path.Combine(data, "statblocks"), Path.Combine(data, "book-index"), maps, Path.Combine(data, "art")),
            TimeSpan.FromMilliseconds(300),
            NullLogger<ContentWatcher>.Instance);
    }

    public void Dispose()
    {
        watcher.Dispose();
        pack.Dispose();
    }

    [Fact]
    public async Task An_edited_document_is_read_again_by_itself()
    {
        await watcher.StartAsync(CancellationToken.None);

        await EditAsync("data/dossiers/npcs.json", "Wenna Brask", "Wenna Mill");

        Assert.True(
            await Eventually(() => live.Current.Dossiers.GetNpcs().Value.Npcs.Any(npc => npc.Name == "Wenna Mill")),
            "The edited NPC never reached the content.");
        Assert.True(await Eventually(() => log.At(LogLevel.Information).Any(line => line.StartsWith("The content changed on disk and was read again", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task A_half_saved_document_is_named_and_the_table_keeps_what_it_had()
    {
        await watcher.StartAsync(CancellationToken.None);
        int npcs = live.Current.Dossiers.GetNpcs().Value.Npcs.Count;
        string path = Path.Combine(pack.Root, "data", "dossiers", "npcs.json");

        await File.WriteAllTextAsync(path, (await File.ReadAllTextAsync(path, CancellationToken.None))[..200], CancellationToken.None);

        Assert.True(await Eventually(() => log.At(LogLevel.Warning).Any(line =>
            line.StartsWith("data/dossiers/npcs.json changed and cannot be read now, so the table keeps the content it had", StringComparison.Ordinal))));
        Assert.Equal(npcs, live.Current.Dossiers.GetNpcs().Value.Npcs.Count);
    }

    [Fact]
    public async Task A_saved_map_is_announced_and_an_edited_art_catalogue_read_again()
    {
        var raised = new ConcurrentQueue<ContentChanges>();
        live.Changed += raised.Enqueue;
        await watcher.StartAsync(CancellationToken.None);

        await EditAsync("Maps/demo/level-1-undercroft.regions.json", "\"levelName\": \"", "\"levelName\": \"Edited ");
        await EditAsync("data/art/catalogue.json", "\"title\": \"", "\"title\": \"Edited ");

        Assert.True(await Eventually(() => raised.Contains(ContentChanges.Maps)), "The map change was never announced.");
        Assert.True(await Eventually(() => art.All().All(image => image.Title.StartsWith("Edited ", StringComparison.Ordinal))), "The art catalogue was never read again.");
    }

    [Fact]
    public async Task Nothing_is_read_before_the_watcher_starts_or_after_it_stops()
    {
        await watcher.StartAsync(CancellationToken.None);
        await watcher.StopAsync(CancellationToken.None);

        await EditAsync("data/dossiers/npcs.json", "Wenna Brask", "Wenna Mill");
        await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.DoesNotContain(live.Current.Dossiers.GetNpcs().Value.Npcs, npc => npc.Name == "Wenna Mill");
    }

    private static async Task<bool> Eventually(Func<bool> condition)
    {
        DateTime until = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < until)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(50, CancellationToken.None);
        }

        return condition();
    }

    private async Task EditAsync(string relative, string from, string to)
    {
        string path = Path.Combine(pack.Root, relative);
        string text = await File.ReadAllTextAsync(path, CancellationToken.None);
        await File.WriteAllTextAsync(path, text.Replace(from, to, StringComparison.Ordinal), CancellationToken.None);
    }
}
