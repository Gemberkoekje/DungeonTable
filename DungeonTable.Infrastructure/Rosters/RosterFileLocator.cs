namespace DungeonTable.Infrastructure.Rosters;

/// <summary>
/// Resolves the roster root directory (holding <c>party.json</c> and <c>allies.json</c>) for both
/// the web host and tests, so neither has to hard-code an absolute path. Mirrors
/// <see cref="Dossiers.DossierFileLocator"/> and <see cref="Stats.StatFileLocator"/>, except that
/// the rosters sit in <c>data/</c> itself rather than a sub-folder — they are two small documents,
/// not a per-level corpus.
/// </summary>
public static class RosterFileLocator
{
    private const string DefaultRelativePath = "data";

    /// <summary>
    /// Resolves the roster root. When <paramref name="configuredPath"/> points at an existing
    /// directory it is used directly; otherwise <c>data</c> under <paramref name="contentRoot"/> is,
    /// if it exists; otherwise the search walks up the directory tree from
    /// <paramref name="startDirectory"/> looking for a <c>data</c> folder.
    /// </summary>
    /// <param name="startDirectory">Directory to begin the upward search from.</param>
    /// <param name="configuredPath">Optional configured path (absolute or relative to the start directory).</param>
    /// <param name="contentRoot">
    /// Optional shared content root (<c>Content:Root</c>) holding every kind of content in its default
    /// layout; absolute or relative to the start directory.
    /// </param>
    /// <returns>The full path to an existing roster directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when no roster directory can be found.</exception>
    public static string Locate(string startDirectory, string configuredPath, string contentRoot = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string full = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(configuredPath, startDirectory);

            if (Directory.Exists(full))
            {
                return full;
            }
        }

        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            string candidate = Path.Combine(Path.GetFullPath(contentRoot, startDirectory), DefaultRelativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        for (DirectoryInfo dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, DefaultRelativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate a '{DefaultRelativePath}' directory under Content:Root or walking up from " +
            $"'{startDirectory}'. Set 'Content:Root' to the folder that holds it, or the 'Rosters:Root' " +
            "configuration key to the directory holding party.json and allies.json.");
    }
}
