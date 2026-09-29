namespace DungeonTable.Infrastructure.Maps;

/// <summary>
/// Resolves the maps root directory (holding per-adventure images and their
/// <c>*.regions.json</c> annotation files) for both the web host and tests, so neither
/// has to hard-code an absolute path. Mirrors <see cref="Dossiers.DossierFileLocator"/>.
/// </summary>
public static class MapFileLocator
{
    private const string DefaultFolderName = "Maps";

    // A candidate folder only counts as the maps root if it actually holds map assets;
    // this stops the walk-up from latching onto an unrelated folder that merely shares the
    // name (for example a source folder called "Maps").
    private static readonly string[] MapAssetExtensions =
        [".ds", ".regions.json", ".webp", ".png", ".jpg", ".jpeg", ".gif"];

    /// <summary>
    /// Resolves the maps root. When <paramref name="configuredPath"/> points at an existing
    /// directory it is used directly; otherwise <c>Maps</c> under <paramref name="contentRoot"/> is,
    /// if it exists; otherwise the search walks up the directory tree from
    /// <paramref name="startDirectory"/> looking for a <c>Maps</c> folder that holds map assets.
    /// </summary>
    /// <param name="startDirectory">Directory to begin the upward search from.</param>
    /// <param name="configuredPath">Optional configured path (absolute or relative to the start directory).</param>
    /// <param name="contentRoot">
    /// Optional shared content root (<c>Content:Root</c>) holding every kind of content in its default
    /// layout; absolute or relative to the start directory.
    /// </param>
    /// <returns>The full path to an existing maps directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when no maps directory can be found.</exception>
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

        // Named on purpose, like the configured path above, so no asset check: an explicitly chosen
        // content root may hold a campaign whose first map is still to be drawn.
        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            string candidate = Path.Combine(Path.GetFullPath(contentRoot, startDirectory), DefaultFolderName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        for (DirectoryInfo dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, DefaultFolderName);
            if (Directory.Exists(candidate) && ContainsMapAssets(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate a '{DefaultFolderName}' directory under Content:Root, or one containing map " +
            $"assets walking up from '{startDirectory}'. Set 'Content:Root' to the folder that holds it, or " +
            "the 'Maps:Root' configuration key to the maps directory itself.");
    }

    private static bool ContainsMapAssets(string directory) =>
        Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Any(path => MapAssetExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
}
