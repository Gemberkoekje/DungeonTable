using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Content;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace DungeonTable.Web.Services;

/// <summary>
/// Watches the content folders and reads the content again when a file in them changes: the
/// documents (a new <see cref="ContentSnapshot"/>), a map's drawing or notes, the committed art
/// catalogue. A JSON file edited by hand or by an LLM then shows at the table without a restart.
/// </summary>
/// <remarks>
/// <para>
/// Changes are gathered for a moment before anything is read: an editor saving a file writes it in
/// more than one step, and a search-and-replace across the folder touches many files at once, and each
/// should cost one reload, not one per write.
/// </para>
/// <para>
/// Watching uses <see cref="PhysicalFileProvider"/>, which polls instead of listening for file events
/// when <c>DOTNET_USE_POLLING_FILE_WATCHER</c> is set: a folder bind-mounted into a container from a
/// Windows or macOS host sends no events. The compose files set it.
/// </para>
/// <para>
/// A reload runs on a pool thread, one at a time; one that fails is logged and the table keeps what it
/// had. Nothing may throw out of the timer's callback, which would take the process down, so the
/// reading runs as a task whose failure is logged by a continuation.
/// </para>
/// </remarks>
public sealed partial class ContentWatcher : IHostedService, IDisposable
{
    private readonly LiveContent live;
    private readonly FileSystemArtCatalogue art;
    private readonly ContentReport report;
    private readonly IReadOnlyList<(string Folder, string Filter, ContentChanges Kind)> watches;
    private readonly TimeSpan settle;
    private readonly ILogger<ContentWatcher> logger;
    private readonly object gate = new object();
    private readonly List<IDisposable> subscriptions = new List<IDisposable>();
    private readonly List<PhysicalFileProvider> providers = new List<PhysicalFileProvider>();
    private readonly Timer timer;
    private ContentChanges pending;
    private bool running;

    /// <summary>Creates the watcher; it watches nothing until the host starts it.</summary>
    /// <param name="live">The documents, read again on a change.</param>
    /// <param name="art">The art catalogue, read again when its committed catalogue changes.</param>
    /// <param name="report">Where each reload is reported.</param>
    /// <param name="folders">What to watch: a folder, a glob under it, and what a change there is.</param>
    /// <param name="settle">How long to wait after the last change before reading.</param>
    /// <param name="logger">Where a reload that failed is reported.</param>
    public ContentWatcher(
        LiveContent live,
        FileSystemArtCatalogue art,
        ContentReport report,
        IReadOnlyList<(string Folder, string Filter, ContentChanges Kind)> folders,
        TimeSpan settle,
        ILogger<ContentWatcher> logger)
    {
        this.live = live;
        this.art = art;
        this.report = report;
        watches = folders ?? Array.Empty<(string, string, ContentChanges)>();
        this.settle = settle;
        this.logger = logger;
        timer = new Timer(_ => ReadAgainAndReport(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>The usual folders of a content root and what a change in each means.</summary>
    /// <param name="dossiersRoot">The dossiers folder.</param>
    /// <param name="statsRoot">The stat block folder.</param>
    /// <param name="bookIndexRoot">The book index folder; empty when there is none.</param>
    /// <param name="mapsRoot">The maps folder.</param>
    /// <param name="artRoot">The art folder.</param>
    /// <returns>The folders to watch.</returns>
    public static IReadOnlyList<(string Folder, string Filter, ContentChanges Kind)> Folders(
        string dossiersRoot, string statsRoot, string bookIndexRoot, string mapsRoot, string artRoot) => new[]
    {
        (dossiersRoot, "*.json", ContentChanges.Documents),
        (statsRoot, "*.json", ContentChanges.Documents),
        (bookIndexRoot ?? string.Empty, "*.json", ContentChanges.Documents),
        (mapsRoot, "**/*.ds", ContentChanges.Maps),
        (mapsRoot, "**/*.regions.json", ContentChanges.Maps),

        // The committed catalogue only: the uploads beside it are the app's own writing.
        (artRoot, "catalogue.json", ContentChanges.Art),
    };

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var byFolder = new Dictionary<string, PhysicalFileProvider>(StringComparer.Ordinal);
        foreach ((string folder, string filter, ContentChanges kind) in watches)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                continue;
            }

            if (!byFolder.TryGetValue(folder, out PhysicalFileProvider provider))
            {
                provider = new PhysicalFileProvider(folder);
                byFolder[folder] = provider;
                providers.Add(provider);
            }

            subscriptions.Add(ChangeToken.OnChange(() => provider.Watch(filter), () => Changed(kind)));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Stops watching.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            timer.Dispose();
            foreach (IDisposable subscription in subscriptions)
            {
                subscription.Dispose();
            }

            foreach (PhysicalFileProvider provider in providers)
            {
                provider.Dispose();
            }

            subscriptions.Clear();
            providers.Clear();
        }
    }

    // A file changed: note what kind, and read once the folder has been quiet for a moment.
    private void Changed(ContentChanges kind)
    {
        lock (gate)
        {
            pending |= kind;
            if (!running)
            {
                TryArm();
            }
        }
    }

    // Caller holds gate.
    private void TryArm()
    {
        try
        {
            timer.Change(settle, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Stopping: nothing more is read.
        }
    }

    // The timer's callback. The reading runs as a task, so a failure in it faults the task rather than
    // escaping onto the pool thread, where it would end the process; the continuation logs it.
    private void ReadAgainAndReport() =>
        _ = ReadAgainAsync().ContinueWith(
            failed => LogReloadFailed(logger, failed.Exception?.GetBaseException()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private async Task ReadAgainAsync()
    {
        ContentChanges changes;
        lock (gate)
        {
            changes = pending;
            pending = ContentChanges.None;
            running = true;
        }

        try
        {
            await ReadAsync(changes).ConfigureAwait(false);
        }
        finally
        {
            lock (gate)
            {
                running = false;

                // Changes that came in while this one read are read next.
                if (pending != ContentChanges.None)
                {
                    TryArm();
                }
            }
        }
    }

    private async Task ReadAsync(ContentChanges changes)
    {
        if (changes.HasFlag(ContentChanges.Documents))
        {
            report.WriteDocumentsReload(live.ReloadDocuments());
        }

        if (changes.HasFlag(ContentChanges.Art))
        {
            report.WriteArtReload(art.ReloadCommitted());
        }

        if (changes.HasFlag(ContentChanges.Maps))
        {
            live.Announce(ContentChanges.Maps);
            await report.WriteMapsReloadAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Reading the content again after a change failed; the table keeps what it had.")]
    private static partial void LogReloadFailed(ILogger logger, Exception error);
}
