using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Content;
using DungeonTable.Infrastructure.Rosters;
using Microsoft.Extensions.Logging;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Web.Services;

/// <summary>
/// Says in the app's log, as it starts, what the content amounts to and everything the stores skipped
/// or dropped reading it: a document that does not parse, an entry with no id, an id written twice, a
/// <c>null</c> in a list, a file under a name nothing reads, a map or a roster that cannot be read. It
/// says the same after each reload, when a document, a map or the art catalogue changed on disk.
/// </summary>
/// <remarks>
/// <para>
/// The stores are fail-soft on purpose, so one slip never takes the table down mid-session; the price
/// was that nothing said what had been skipped, and a tab was just quietly short. The content tests
/// say it, but they need the .NET SDK, which someone running the app from its image does not have.
/// The log does not.
/// </para>
/// <para>
/// It runs in the host's Starting phase, before the app takes its first request, which also means the
/// content is read at startup rather than by whoever opens the DM screen first.
/// </para>
/// </remarks>
public sealed partial class ContentReport : IHostedLifecycleService
{
    private readonly LiveContent live;
    private readonly FileSystemArtCatalogue art;
    private readonly IMapStore maps;
    private readonly IVectorMapStore drawings;
    private readonly FileSystemPartyRoster party;
    private readonly FileSystemAllyRoster allies;
    private readonly string contentRoot;
    private readonly ILogger<ContentReport> logger;

    /// <summary>Creates the report over the stores it asks.</summary>
    /// <param name="live">The documents: dossiers, stat blocks and the book index.</param>
    /// <param name="art">The art catalogue.</param>
    /// <param name="maps">The maps' notes, as the Room Editor writes them.</param>
    /// <param name="drawings">The maps' drawings.</param>
    /// <param name="party">The party roster file.</param>
    /// <param name="allies">The ally roster file.</param>
    /// <param name="contentRoot">The folder a file is named from in the log; a file outside it is named in full.</param>
    /// <param name="logger">Where the report goes.</param>
    public ContentReport(
        LiveContent live,
        FileSystemArtCatalogue art,
        IMapStore maps,
        IVectorMapStore drawings,
        FileSystemPartyRoster party,
        FileSystemAllyRoster allies,
        string contentRoot,
        ILogger<ContentReport> logger)
    {
        this.live = live;
        this.art = art;
        this.maps = maps;
        this.drawings = drawings;
        this.party = party;
        this.allies = allies;
        this.contentRoot = contentRoot ?? string.Empty;
        this.logger = logger;
    }

    /// <summary>Everything the stores skipped or dropped, maps and rosters included, in a stable order.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The problems (empty when there are none).</returns>
    public async Task<IReadOnlyList<ContentProblem>> ProblemsAsync(CancellationToken cancellationToken)
    {
        var problems = new List<ContentProblem>();
        problems.AddRange(live.Current.Problems);
        problems.AddRange(art.Problems);
        problems.AddRange(await MapProblemsAsync(cancellationToken).ConfigureAwait(false));

        Result<Party> partyRoster = party.GetParty();
        if (!partyRoster.IsValid)
        {
            problems.Add(new ContentProblem("party.json", Messages(partyRoster)));
        }

        Result<AllyRoster> allyRoster = allies.GetAllies();
        if (!allyRoster.IsValid)
        {
            problems.Add(new ContentProblem("allies.json", Messages(allyRoster)));
        }

        return problems;
    }

    /// <summary>Writes the report to the log: what the content amounts to, then each problem.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task WriteAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ContentProblem> problems = await ProblemsAsync(cancellationToken).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Information))
        {
            string from = contentRoot.Length > 0 ? contentRoot : "the folders found by walking up";
            ContentSnapshot now = live.Current;
            int areas = now.Dossiers.AllAreas().Count;
            int levels = now.Dossiers.AllLevels().Count;
            int quests = Count(now.Dossiers.GetQuests(), log => log.Quests.Count);
            int npcs = Count(now.Dossiers.GetNpcs(), roster => roster.Npcs.Count);
            int monsters = now.Stats.AllMonsters().Count;
            int spells = now.Stats.AllSpells().Count;
            int pictures = art.All().Count;
            // The drawn maps, as the map pickers count them: a map counts before the Room Editor has
            // saved a .regions.json beside it.
            int mapCount = drawings.ListMapIds().Count;
            LogContent(logger, from, areas, levels, quests, npcs, monsters, spells, pictures, mapCount);
        }

        WriteProblems(problems);
    }

    /// <summary>Says what a reload of the documents did, and what its reading skipped or dropped.</summary>
    /// <param name="reload">The reload.</param>
    public void WriteDocumentsReload(ContentReload reload)
    {
        ArgumentNullException.ThrowIfNull(reload);

        if (!reload.Applied)
        {
            foreach (ContentProblem blocking in reload.Blocking)
            {
                LogKept(logger, Named(blocking.File), blocking.Reason);
            }

            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            long milliseconds = (long)reload.Took.TotalMilliseconds;
            int areas = live.Current.Dossiers.AllAreas().Count;
            LogReloaded(logger, milliseconds, areas);
        }

        WriteProblems(reload.Problems);
    }

    /// <summary>Says that the maps were read again, and names any that cannot be read now.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task WriteMapsReloadAsync(CancellationToken cancellationToken)
    {
        LogMapsReloaded(logger);
        WriteProblems(await MapProblemsAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Says what a reload of the committed art catalogue did.</summary>
    /// <param name="found">What the new reading skipped or dropped.</param>
    public void WriteArtReload(IReadOnlyList<ContentProblem> found)
    {
        ArgumentNullException.ThrowIfNull(found);

        ContentProblem kept = found.FirstOrDefault(problem => problem.DocumentSkipped);
        if (kept is not null)
        {
            LogKept(logger, Named(kept.File), kept.Reason);
            return;
        }

        LogArtReloaded(logger);
        WriteProblems(found);
    }

    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken) => WriteAsync(cancellationToken);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Each map's notes and drawing, loaded as the DM screen would, so a broken one is named before
    // anyone opens it.
    private async Task<IReadOnlyList<ContentProblem>> MapProblemsAsync(CancellationToken cancellationToken)
    {
        var problems = new List<ContentProblem>();
        foreach (string mapId in maps.ListMapIds())
        {
            Result<MapDefinition> notes = await maps.LoadAsync(mapId, cancellationToken).ConfigureAwait(false);
            if (!notes.IsValid)
            {
                problems.Add(new ContentProblem($"{mapId}.regions.json", Messages(notes) + " The DM screen marks no rooms on this map, and the projector hides it."));
            }
        }

        foreach (string mapId in drawings.ListMapIds())
        {
            Result<VectorMap> drawing = await drawings.LoadAsync(mapId, cancellationToken).ConfigureAwait(false);
            if (!drawing.IsValid)
            {
                problems.Add(new ContentProblem($"{mapId}.ds", Messages(drawing) + " The map cannot be shown."));
            }
        }

        return problems;
    }

    private void WriteProblems(IReadOnlyList<ContentProblem> problems)
    {
        foreach (ContentProblem problem in problems)
        {
            LogProblem(logger, Named(problem.File), problem.Problem);
        }

        if (problems.Count == 1)
        {
            LogOneProblem(logger);
        }
        else if (problems.Count > 1)
        {
            LogProblemCount(logger, problems.Count);
        }
    }

    // A file under the content root by its path from there ("data/dossiers/npcs.json"), anything else
    // as it was found.
    private string Named(string file)
    {
        if (contentRoot.Length == 0 || !Path.IsPathRooted(file))
        {
            return file;
        }

        string relative = Path.GetRelativePath(contentRoot, file);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? file
            : relative.Replace('\\', '/');
    }

    private static int Count<T>(Result<T> read, Func<T, int> count) => read.IsValid ? count(read.Value) : 0;

    private static string Messages(Result result) =>
        string.Join(" ", result.Messages.Select(message => message.Message));

    // Source-generated, like TablePersistence's, for the analyzers (CA1848 / CA1873).
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Content read from {ContentRoot}. Areas: {Areas}, floors: {Levels}, quests: {Quests}, NPCs: {Npcs}, "
            + "stat blocks: {Monsters}, spells: {Spells}, pictures: {Pictures}, maps: {Maps}.")]
    private static partial void LogContent(
        ILogger logger,
        string contentRoot,
        int areas,
        int levels,
        int quests,
        int npcs,
        int monsters,
        int spells,
        int pictures,
        int maps);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content problem in {File}: {Problem}")]
    private static partial void LogProblem(ILogger logger, string file, string problem);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Count} content problems, listed above. The app skipped or dropped what each one names, so "
            + "a tab may look short until it is fixed. The content tests check a campaign in full.")]
    private static partial void LogProblemCount(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "1 content problem, listed above. The app skipped or dropped what it names, so a tab may "
            + "look short until it is fixed. The content tests check a campaign in full.")]
    private static partial void LogOneProblem(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The content changed on disk and was read again in {Milliseconds} ms (areas: {Areas}). "
            + "Open pages show it now.")]
    private static partial void LogReloaded(ILogger logger, long milliseconds, int areas);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{File} changed and cannot be read now, so the table keeps the content it had until the file "
            + "is fixed and saved again: {Reason}")]
    private static partial void LogKept(ILogger logger, string file, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "A map changed on disk; open pages read it again.")]
    private static partial void LogMapsReloaded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "The art catalogue changed on disk and was read again.")]
    private static partial void LogArtReloaded(ILogger logger);
}
