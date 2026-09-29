using System.Collections.Generic;
using System.IO;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Books;
using DungeonTable.Infrastructure.Dossiers;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Infrastructure.Rosters;
using DungeonTable.Infrastructure.Stats;

namespace DungeonTable.ContentTests;

/// <summary>
/// The content under test: the folder <c>DUNGEONTABLE_CONTENT_ROOT</c> names, or the sample pack
/// (<c>samples/demo</c>) when it is unset, loaded through the app's own locators and stores so the
/// rules check what the app will show.
/// </summary>
/// <remarks>
/// <para>
/// A relative <c>DUNGEONTABLE_CONTENT_ROOT</c> is read from the test process's working directory, so
/// an absolute path is the safer thing to set.
/// </para>
/// <para>
/// Each kind of content is found the way the app finds it with <c>Content:Root</c> set, except that
/// the locators' walk-up fallback starts in an empty folder. A kind the content root lacks is then
/// missing here as it would be for the app, rather than found in whatever checkout the tests run in.
/// </para>
/// </remarks>
internal static class ContentRoot
{
    /// <summary>The environment variable that names the content root.</summary>
    internal const string Variable = "DUNGEONTABLE_CONTENT_ROOT";

    private static readonly Lazy<string> Root = new(() => Resolve(Environment.GetEnvironmentVariable(Variable), AppContext.BaseDirectory));
    private static readonly Lazy<string> Start = new(EmptyFolder);
    private static readonly Lazy<FileSystemDossierStore> LoadedDossiers = new(() => new FileSystemDossierStore(DossiersFolder));
    private static readonly Lazy<FileSystemStatLibrary> LoadedStats = new(() => new FileSystemStatLibrary(StatsFolder));
    private static readonly Lazy<IReadOnlyList<BookIndex>> LoadedBooks = new(() => new FileSystemBookIndexStore(BookIndexFolder).AllBooks());
    private static readonly Lazy<LinkTargets> BuiltTargets = new(() => LinkTargets.Build(Dossiers, Stats, Books));
    private static readonly Lazy<AuthoredProjection> BuiltProjection = new(() => new AuthoredProjection(Dossiers, Stats, Targets, Books));

    /// <summary>The content root: every kind of content in its usual layout (<c>Maps/</c>, <c>data/</c>).</summary>
    internal static string Folder => Root.Value;

    /// <summary>Which content this is, for a message that has to say what was checked.</summary>
    internal static string Described =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable))
            ? $"the sample pack at {Folder}"
            : $"{Variable}={Folder}";

    /// <summary>Where the locators' walk-up fallback starts: an empty folder.</summary>
    internal static string StartFolder => Start.Value;

    /// <summary>The maps folder, as the app finds it.</summary>
    internal static string MapsFolder => MapFileLocator.Locate(Start.Value, configuredPath: null, Folder);

    /// <summary>The dossiers folder, as the app finds it.</summary>
    internal static string DossiersFolder => DossierFileLocator.Locate(Start.Value, configuredPath: null, Folder);

    /// <summary>The stat-block folder, as the app finds it.</summary>
    internal static string StatsFolder => StatFileLocator.Locate(Start.Value, configuredPath: null, Folder);

    /// <summary>The art folder, as the app finds it.</summary>
    internal static string ArtFolder => ArtFileLocator.Locate(Start.Value, configuredPath: null, Folder);

    /// <summary>The folder holding the roster seeds, as the app finds it.</summary>
    internal static string RostersFolder => RosterFileLocator.Locate(Start.Value, configuredPath: null, Folder);

    /// <summary>The book index folder, or an empty string: a campaign may have none.</summary>
    internal static string BookIndexFolder => BookIndexFileLocator.Locate(Start.Value, configuredPath: null, Folder);

    /// <summary>The dossiers, loaded as the app loads them.</summary>
    internal static FileSystemDossierStore Dossiers => LoadedDossiers.Value;

    /// <summary>The stat blocks and spells, loaded as the app loads them.</summary>
    internal static FileSystemStatLibrary Stats => LoadedStats.Value;

    /// <summary>The book index, loaded as the app loads it.</summary>
    internal static IReadOnlyList<BookIndex> Books => LoadedBooks.Value;

    /// <summary>Everything a link can name, indexed as the app indexes it.</summary>
    internal static LinkTargets Targets => BuiltTargets.Value;

    /// <summary>The briefings, cards and search the app builds from this content.</summary>
    internal static AuthoredProjection Projection => BuiltProjection.Value;

    /// <summary>The party roster seed.</summary>
    internal static FileSystemPartyRoster Party => new(RostersFolder);

    /// <summary>The ally roster seed.</summary>
    internal static FileSystemAllyRoster Allies => new(RostersFolder);

    /// <summary>A file's path relative to the content root, with forward slashes, for a message.</summary>
    /// <param name="fullPath">The file's full path.</param>
    /// <returns>The relative path ("data/dossiers/level-1.json").</returns>
    internal static string NameOf(string fullPath) => Path.GetRelativePath(Folder, fullPath).Replace('\\', '/');

    /// <summary>
    /// The content root: the folder <paramref name="configured"/> names, or else the sample pack, found
    /// by walking up from <paramref name="searchFrom"/>.
    /// </summary>
    /// <param name="configured">The value of <c>DUNGEONTABLE_CONTENT_ROOT</c>: absolute, or relative to the working directory.</param>
    /// <param name="searchFrom">Where to start looking for the sample pack: the test output.</param>
    /// <returns>The content root's full path.</returns>
    /// <exception cref="DirectoryNotFoundException">When the variable names no folder, or there is no sample pack to fall back on.</exception>
    internal static string Resolve(string configured, string searchFrom)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string full = Path.GetFullPath(configured.Trim());
            return Directory.Exists(full)
                ? full
                : throw new DirectoryNotFoundException(
                    $"{Variable} is '{configured}', and there is no folder at '{full}'. Point it at a content root: the folder that holds Maps/ and data/.");
        }

        for (DirectoryInfo dir = new DirectoryInfo(searchFrom); dir is not null; dir = dir.Parent)
        {
            string pack = Path.Combine(dir.FullName, "samples", "demo");
            if (Directory.Exists(Path.Combine(pack, "data")))
            {
                return pack;
            }
        }

        throw new DirectoryNotFoundException(
            $"{Variable} is not set, and there is no sample pack (samples/demo) above '{searchFrom}'.");
    }

    // Where the locators' walk-up fallback starts. Nothing is ever written here.
    private static string EmptyFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dungeontable-content-tests-start");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
