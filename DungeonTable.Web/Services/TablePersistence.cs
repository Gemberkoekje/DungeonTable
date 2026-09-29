using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Session;
using Microsoft.Extensions.Logging;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Web.Services;

/// <summary>
/// Keeps the one live table in the database: restores it before the app serves its first request, and
/// writes it back whenever the reveal state or the fight changes. This is what makes an app restart
/// survivable — without it a crash, a deploy or a laptop reboot mid-combat costs the evening.
/// </summary>
/// <remarks>
/// <para>
/// <b>Debounced, not per change.</b> Painting fog raises <c>Changed</c> per cell and rolling
/// initiative raises it per row; writing the whole document each time would be pointless traffic
/// while the DM drags a brush. Changes only set a dirty flag, and a <see cref="SaveInterval"/> tick
/// writes at most one document per interval. A failed write leaves the flag set, so a database that
/// blinks recovers by itself on the next tick.
/// </para>
/// <para>
/// <b>Restore happens in <see cref="StartingAsync"/></b>, not in <c>StartAsync</c> or
/// <c>ExecuteAsync</c>. Kestrel is itself a hosted service and is registered before this one, so by
/// the time our <c>StartAsync</c> ran the server would already be accepting requests — a DM circuit
/// could initialise and start seeding a map into state that was about to be overwritten. The
/// <c>Starting</c> phase runs before <em>every</em> hosted service's <c>StartAsync</c>, which is
/// exactly the ordering guarantee this needs.
/// </para>
/// <para>
/// Change notifications are subscribed to only <em>after</em> the restore, so restoring does not
/// immediately mark the table dirty and write back what was just read.
/// </para>
/// </remarks>
public sealed partial class TablePersistence : BackgroundService, IHostedLifecycleService
{
    /// <summary>How long changes are allowed to pile up before the table is written.</summary>
    public static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(2);

    // A shutdown save runs on its own deadline rather than the host's shutdown token: that token may
    // already be cancelled when the host is stopping in a hurry, and losing the final write is the one
    // outcome this whole service exists to prevent.
    private static readonly TimeSpan ShutdownSaveTimeout = TimeSpan.FromSeconds(5);

    private readonly ISessionStore store;
    private readonly SessionState session;
    private readonly BattleState battle;
    private readonly CampaignState campaign;
    private readonly ILogger<TablePersistence> logger;
    private readonly string sessionId;

    private readonly object gate = new object();
    private bool dirty;
    private bool blocked;
    private bool subscribed;
    private TableSaveStatus status = new TableSaveStatus();

    /// <summary>Creates the coordinator over the store and the three live singletons it persists.</summary>
    /// <param name="store">The session store adapter (Marten, or the ephemeral fallback).</param>
    /// <param name="session">The shared reveal state.</param>
    /// <param name="battle">The one live fight.</param>
    /// <param name="campaign">Quest progress, each deck's drawn cards, and the areas' progress.</param>
    /// <param name="sessionId">The document key for this table.</param>
    /// <param name="logger">Logger for restore/save outcomes.</param>
    public TablePersistence(
        ISessionStore store,
        SessionState session,
        BattleState battle,
        CampaignState campaign,
        string sessionId,
        ILogger<TablePersistence> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(logger);

        this.store = store;
        this.session = session;
        this.battle = battle;
        this.campaign = campaign;
        this.sessionId = sessionId;
        this.logger = logger;

        status = store.IsPersistent
            ? Describe(TableSaveKind.Waiting, default, "The table has not been saved yet.")
            : Describe(
                TableSaveKind.Disabled,
                default,
                "No database is configured (ConnectionStrings:Postgres), so this table will be lost "
                + "when the app stops.");
    }

    /// <summary>Raised whenever the save status changes, so the DM screen's status bar re-renders.</summary>
    public event Action Changed;

    /// <summary>Where the table stands with the database right now.</summary>
    public TableSaveStatus Status()
    {
        lock (gate) { return status; }
    }

    /// <summary>True when there are changes waiting to be written.</summary>
    public bool HasUnsavedChanges
    {
        get { lock (gate) { return dirty; } }
    }

    /// <summary>
    /// Restores the stored table and starts listening for changes. Runs before any hosted service —
    /// including the web server — has started.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the table has been restored.</returns>
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (store.IsPersistent)
        {
            await RestoreAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            LogNotPersisting(logger);
        }

        Subscribe();
    }

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Writes any outstanding changes before the app goes away.</summary>
    /// <param name="cancellationToken">Cancellation token (deliberately not used for the save).</param>
    /// <returns>A task that completes once the final save has been attempted.</returns>
    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        Unsubscribe();
        using var deadline = new CancellationTokenSource(ShutdownSaveTimeout);
        await FlushAsync(deadline.Token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Loads the stored table and puts it back into the live services. Public so a test (and the
    /// hosted-service start) can drive it directly.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when a stored table was restored.</returns>
    public async Task<bool> RestoreAsync(CancellationToken cancellationToken)
    {
        if (!store.IsPersistent)
        {
            return false;
        }

        Result<TableSnapshot> stored = await store.LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (!stored.IsValid)
        {
            // Nothing stored yet is the normal first-run case, so this is information, not a warning.
            // The reason is shown to the DM as well as logged: "no stored table yet" and "the stored
            // table could not be read" both land here and are worth telling apart at a glance.
            string missing = Explain(stored.Messages);
            LogNothingRestored(logger, sessionId, missing);
            Publish(Describe(
                TableSaveKind.Waiting,
                default,
                $"Nothing was restored ({missing}) — changes from here are being saved."));
            return false;
        }

        TableSnapshot snapshot = stored.Value;
        if (snapshot.Version > TableSnapshot.CurrentVersion)
        {
            // A newer build has been here. Restoring it would be guesswork and saving over it would
            // destroy state this build cannot read, so do neither and say so loudly.
            lock (gate)
            {
                blocked = true;
            }

            LogNewerDocument(logger, sessionId, snapshot.Version, TableSnapshot.CurrentVersion);

            Publish(Describe(
                TableSaveKind.Blocked,
                snapshot.SavedAt,
                $"The stored table was written by a newer version ({snapshot.Version}) and is not "
                + "being read or overwritten. This table is not being saved."));
            return false;
        }

        session.Restore(snapshot.Reveals);
        battle.Restore(snapshot.Battle);

        // Absent on a document written before the quest log existed: restore an empty campaign so the
        // quest log starts clean rather than skipping the call and leaving whatever this process
        // happened to hold.
        campaign.Restore(snapshot.Campaign ?? new CampaignSnapshot());

        RevealSnapshot reveals = snapshot.Reveals ?? new RevealSnapshot();
        BattleSnapshot fight = snapshot.Battle ?? new BattleSnapshot();
        CampaignSnapshot quests = snapshot.Campaign ?? new CampaignSnapshot();
        LogRestored(
            logger,
            sessionId,
            snapshot.SavedAt,
            reveals.CurrentMapId,
            reveals.RevealedRegionIds.Count,
            reveals.FogCells.Count,
            fight.Combatants.Count,
            quests.Quests.Count);

        Publish(Describe(
            TableSaveKind.Saved,
            snapshot.SavedAt,
            "Restored from the database; every change since is being saved."));
        return true;
    }

    /// <summary>
    /// Writes the table if anything has changed since the last write. The save loop calls this on a
    /// timer; a test calls it directly rather than waiting for a tick.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when a document was written.</returns>
    public async Task<bool> FlushAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (!store.IsPersistent || blocked || !dirty)
            {
                return false;
            }

            // Cleared before the capture, so a change arriving mid-save marks the table dirty again
            // and is written by the next tick instead of being swallowed by this one.
            dirty = false;
        }

        TableSnapshot snapshot = Capture();
        Result saved = await store.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
        if (saved.IsValid)
        {
            bool recovered;
            lock (gate)
            {
                recovered = status.Kind == TableSaveKind.Failed;
            }

            if (recovered)
            {
                LogSaveRecovered(logger, sessionId);
            }

            Publish(Describe(TableSaveKind.Saved, snapshot.SavedAt, "Every change is being saved."));
            return true;
        }

        string reason = Explain(saved.Messages);
        bool firstFailure;
        DateTimeOffset lastSaved;
        lock (gate)
        {
            // Put the flag back so the next tick retries; a blip should heal without the DM doing
            // anything. Only the first failure of a run is logged as a warning — a database that stays
            // down would otherwise write a line every couple of seconds all evening.
            dirty = true;
            firstFailure = status.Kind != TableSaveKind.Failed;

            // Keep pointing at the last write that did succeed: "failed, and the table on disk is from
            // 20:31" is far more use to a DM than "failed" with no date.
            lastSaved = status.At;
        }

        if (firstFailure)
        {
            LogSaveFailed(logger, sessionId, reason, SaveInterval.TotalSeconds);
        }

        Publish(Describe(
            TableSaveKind.Failed,
            lastSaved,
            $"The last save failed and is being retried: {reason}"));
        return false;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!store.IsPersistent)
        {
            // Nothing to write and nothing to poll for; the status already says the table is not saved.
            return;
        }

        using var timer = new PeriodicTimer(SaveInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await FlushAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down. StoppingAsync has already taken the final save.
        }
    }

    /// <summary>Unsubscribes from the live state.</summary>
    public override void Dispose()
    {
        Unsubscribe();
        base.Dispose();
    }

    private TableSnapshot Capture() => new TableSnapshot
    {
        SessionId = sessionId,
        Version = TableSnapshot.CurrentVersion,
        SavedAt = DateTimeOffset.UtcNow,
        Reveals = session.Capture(),
        Battle = battle.Capture(),
        Campaign = campaign.Capture(),
    };

    private void Subscribe()
    {
        lock (gate)
        {
            if (subscribed)
            {
                return;
            }

            subscribed = true;
        }

        session.Changed += MarkDirty;
        battle.Changed += MarkDirty;
        campaign.Changed += MarkDirty;
    }

    private void Unsubscribe()
    {
        lock (gate)
        {
            if (!subscribed)
            {
                return;
            }

            subscribed = false;
        }

        session.Changed -= MarkDirty;
        battle.Changed -= MarkDirty;
        campaign.Changed -= MarkDirty;
    }

    private void MarkDirty()
    {
        lock (gate)
        {
            dirty = true;
        }
    }

    private static TableSaveStatus Describe(TableSaveKind kind, DateTimeOffset at, string message) =>
        new TableSaveStatus { Kind = kind, At = at, Message = message };

    private void Publish(TableSaveStatus next)
    {
        bool changed;
        lock (gate)
        {
            changed = status.Kind != next.Kind || status.At != next.At;
            status = next;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private static string Explain(IEnumerable<IValidationMessage> messages)
    {
        string joined = string.Join(" ", messages.Select(message => message.Message));
        return joined.Length > 0 ? joined : "no reason given";
    }

    // ---- Logging ----------------------------------------------------------------------------
    //
    // Source-generated log methods rather than direct logger.LogX calls: the generator emits the
    // IsEnabled guard and the strongly-typed state object, which is what satisfies the analyzers this
    // solution builds warnings-as-errors with (CA1848 / CA1873) without suppressing either of them.

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No \"ConnectionStrings:Postgres\" is configured, so the reveal state and any "
            + "running fight live only in memory: they survive a browser refresh but not an app "
            + "restart. The DM screen shows \"not saved\" for this whole session.")]
    private static partial void LogNotPersisting(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "No table restored for session {SessionId}: {Reason}")]
    private static partial void LogNothingRestored(ILogger logger, string sessionId, string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The stored table for session {SessionId} was written by a newer version "
            + "({StoredVersion} > {SupportedVersion}). It has not been restored, and saving is "
            + "suspended so it is not overwritten.")]
    private static partial void LogNewerDocument(
        ILogger logger,
        string sessionId,
        int storedVersion,
        int supportedVersion);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Restored the table for session {SessionId} saved at {SavedAt}: map {MapId}, "
            + "{RegionCount} revealed regions, {CellCount} fog cells, {CombatantCount} combatants, "
            + "{QuestCount} tracked quests.")]
    private static partial void LogRestored(
        ILogger logger,
        string sessionId,
        DateTimeOffset savedAt,
        string mapId,
        int regionCount,
        int cellCount,
        int combatantCount,
        int questCount);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Could not save the table for session {SessionId}: {Reason}. Retrying every "
            + "{IntervalSeconds}s.")]
    private static partial void LogSaveFailed(
        ILogger logger,
        string sessionId,
        string reason,
        double intervalSeconds);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Saving the table for session {SessionId} recovered.")]
    private static partial void LogSaveRecovered(ILogger logger, string sessionId);
}
