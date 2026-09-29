using DungeonTable.Core.Battle;
using DungeonTable.Core.Session;
using JasperFx;
using Marten;
using Marten.Services;
using Weasel.Core;

namespace DungeonTable.Infrastructure.Sessions;

/// <summary>
/// The stored contract for the session document: which schema it lives in, what its key is, and
/// exactly how it is serialized.
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>configuration function, not a DI extension</b> — the composition root still does all
/// the registering (<c>builder.Services.AddMarten(SessionDocuments.Configure)</c>). It exists so the
/// serializer has one definition: the round-trip test builds it from <see cref="Serializer"/> and so
/// checks the shape the database will really hold, instead of a second set of options that agrees
/// with this one until the day it does not.
/// </para>
/// <para>
/// Enums are stored <b>as strings</b> deliberately. A condition or combatant kind written as an
/// integer would silently change meaning the moment a member is inserted into the middle of the enum
/// — and both enums start with a <c>None</c> member that exists precisely so a value is never
/// implicitly zero-shaped.
/// </para>
/// </remarks>
public static class SessionDocuments
{
    /// <summary>The Postgres schema the session document lives in, kept out of <c>public</c>.</summary>
    public const string SchemaName = "dungeontable";

    /// <summary>
    /// Configures a Marten store to hold the session document: the connection, the schema, and the
    /// document's string identity.
    /// </summary>
    /// <param name="options">The store options to configure.</param>
    /// <param name="connectionString">The Postgres connection string.</param>
    public static void Configure(StoreOptions options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        options.Connection(connectionString);
        options.DatabaseSchemaName = SchemaName;

        // The app owns its schema and there are no migrations to run: one document type, created or
        // updated from the type itself on first use.
        options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;

        options.Serializer(Serializer());
        options.Schema.For<TableSnapshot>().Identity(snapshot => snapshot.SessionId);

        // The party and ally rosters, keyed by the same table identity. They are separate documents
        // rather than fields on the table snapshot on purpose: the snapshot is rewritten on a
        // debounce while a fight runs, and a roster edited once an evening has no business riding
        // along with it.
        options.Schema.For<StoredParty>().Identity(stored => stored.SessionId);
        options.Schema.For<StoredAllies>().Identity(stored => stored.SessionId);
    }

    /// <summary>
    /// Builds the serializer the session document is stored with: camelCase names and string enums.
    /// </summary>
    /// <returns>A serializer instance; Marten keeps the one it is given.</returns>
    // Both the interface AND the implementation are spelled out: Weasel.Core has a type of each
    // name and both namespaces are in scope here (Marten's public API leans on Weasel for
    // EnumStorage / Casing). Weasel gained its own SystemTextJsonSerializer in the version Marten
    // 9.37 pulls in, which is why the concrete type needs qualifying too now.
    public static Marten.ISerializer Serializer() => new Marten.Services.SystemTextJsonSerializer
    {
        EnumStorage = EnumStorage.AsString,
        Casing = Casing.CamelCase,
    };
}
