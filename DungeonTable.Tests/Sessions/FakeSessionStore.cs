using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Session;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Sessions;

/// <summary>
/// In-memory session store, so the persistence coordinator can be tested without Postgres: it counts
/// writes, hands back a document on demand, and can be told to fail.
/// </summary>
internal sealed class FakeSessionStore : ISessionStore
{
    /// <summary>What <see cref="LoadAsync"/> serves; null means "nothing stored".</summary>
    public TableSnapshot Stored { get; set; }

    /// <summary>The last snapshot written.</summary>
    public TableSnapshot Saved { get; private set; }

    /// <summary>How many times a write was attempted.</summary>
    public int SaveCount { get; private set; }

    /// <summary>How many times a read was attempted.</summary>
    public int LoadCount { get; private set; }

    /// <summary>When true, every write fails.</summary>
    public bool FailSaves { get; set; }

    /// <inheritdoc />
    public bool IsPersistent { get; set; } = true;

    /// <inheritdoc />
    public Task<Result<TableSnapshot>> LoadAsync(string sessionId, CancellationToken cancellationToken)
    {
        LoadCount++;
        return Task.FromResult(Stored is null
            ? Result.WithMessages<TableSnapshot>(ValidationMessage.Error("Nothing stored.", nameof(sessionId)))
            : Result.For(Stored));
    }

    /// <inheritdoc />
    public Task<Result> SaveAsync(TableSnapshot snapshot, CancellationToken cancellationToken)
    {
        SaveCount++;
        if (FailSaves)
        {
            return Task.FromResult(Result.WithMessages(
                ValidationMessage.Error("The database is down.", nameof(snapshot))));
        }

        Saved = snapshot;
        Stored = snapshot;
        return Task.FromResult(Result.OK);
    }
}
