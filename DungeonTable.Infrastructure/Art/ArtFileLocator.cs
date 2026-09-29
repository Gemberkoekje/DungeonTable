namespace DungeonTable.Infrastructure.Art;

/// <summary>
/// Resolves the art root directory (holding the committed <c>catalogue.json</c> and the book art
/// beside it) for both the web host and tests, so neither has to hard-code an absolute path.
/// Mirrors <see cref="Stats.StatFileLocator"/>.
/// </summary>
public static class ArtFileLocator
{
    private const string DefaultRelativePath = "data/art";

    /// <summary>
    /// Resolves the art root. When <paramref name="configuredPath"/> points at an existing
    /// directory it is used directly; otherwise <c>data/art</c> under <paramref name="contentRoot"/>
    /// is, if it exists; otherwise the search walks up the directory tree from
    /// <paramref name="startDirectory"/> looking for a <c>data/art</c> folder.
    /// </summary>
    /// <param name="startDirectory">Directory to begin the upward search from.</param>
    /// <param name="configuredPath">Optional configured path (absolute or relative to the start directory).</param>
    /// <param name="contentRoot">
    /// Optional shared content root (<c>Content:Root</c>) holding every kind of content in its default
    /// layout; absolute or relative to the start directory.
    /// </param>
    /// <returns>The full path to an existing art directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when no art directory can be found.</exception>
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
            string candidate = Path.Combine(Path.GetFullPath(contentRoot, startDirectory), "data", "art");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        for (DirectoryInfo dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "data", "art");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate a '{DefaultRelativePath}' directory under Content:Root or walking up from " +
            $"'{startDirectory}'. Set 'Content:Root' to the folder that holds it, or the 'Art:Root' " +
            "configuration key to the art directory itself.");
    }
}
