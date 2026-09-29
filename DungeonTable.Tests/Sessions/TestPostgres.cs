namespace DungeonTable.Tests.Sessions;

/// <summary>
/// Where the persistence integration tests get their database — and how they get out of the way when
/// there is not one.
/// </summary>
/// <remarks>
/// <para>
/// The suite must stay green on a machine with no PostgreSQL, exactly as the app itself runs without
/// one, so these cases <b>skip</b> rather than fail unless <c>DUNGEONTABLE_TEST_POSTGRES</c> holds a
/// connection string:
/// </para>
/// <code>
/// docker compose up -d postgres
/// $env:DUNGEONTABLE_TEST_POSTGRES = "Host=localhost;Port=5432;Database=dungeontable;Username=dungeontable;Password=..."
/// dotnet test
/// </code>
/// </remarks>
internal static class TestPostgres
{
    /// <summary>The environment variable holding the test connection string.</summary>
    internal const string ConnectionVariable = "DUNGEONTABLE_TEST_POSTGRES";

    /// <summary>
    /// The configured test connection string, skipping the calling test when there is none.
    /// </summary>
    /// <returns>The connection string.</returns>
    internal static string RequireConnection()
    {
        string connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(connection),
            $"Set {ConnectionVariable} to a PostgreSQL connection string to run the persistence "
            + "integration tests (docker compose up -d postgres).");

        return connection;
    }
}
