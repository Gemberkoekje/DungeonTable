namespace DungeonTable.Infrastructure.Dossiers;

/// <summary>
/// Resolves the dossiers root directory (holding the committed <c>level-{n}.json</c> and
/// <c>reference.json</c> documents) for both the web host and tests, so neither has to hard-code
/// an absolute path. Mirrors <see cref="Maps.MapFileLocator"/> and
/// <see cref="Stats.StatFileLocator"/>.
/// </summary>
public static class DossierFileLocator
{
    private const string DefaultRelativePath = "data/dossiers";

    /// <summary>
    /// Resolves the dossiers root. When <paramref name="configuredPath"/> points at an existing
    /// directory it is used directly; otherwise <c>data/dossiers</c> under
    /// <paramref name="contentRoot"/> is, if it exists; otherwise the search walks up the directory
    /// tree from <paramref name="startDirectory"/> looking for a <c>data/dossiers</c> folder.
    /// </summary>
    /// <param name="startDirectory">Directory to begin the upward search from.</param>
    /// <param name="configuredPath">Optional configured path (absolute or relative to the start directory).</param>
    /// <param name="contentRoot">
    /// Optional shared content root (<c>Content:Root</c>) holding every kind of content in its default
    /// layout; absolute or relative to the start directory.
    /// </param>
    /// <returns>The full path to an existing dossiers directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when no dossiers directory can be found.</exception>
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
            string candidate = Path.Combine(Path.GetFullPath(contentRoot, startDirectory), "data", "dossiers");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        for (DirectoryInfo dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "data", "dossiers");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate a '{DefaultRelativePath}' directory under Content:Root or walking up from " +
            $"'{startDirectory}'. Set 'Content:Root' to the folder that holds it, or the 'Dossiers:Root' " +
            "configuration key to the dossiers directory itself.");
    }
}
