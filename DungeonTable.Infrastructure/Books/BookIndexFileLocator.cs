namespace DungeonTable.Infrastructure.Books;

/// <summary>
/// Resolves the book index directory (<c>data/book-index</c>, one <c>&lt;book&gt;.json</c> per book)
/// the way <see cref="Dossiers.DossierFileLocator"/> resolves the dossiers: its own key, then the
/// shared content root, then walking up from the start directory.
/// </summary>
/// <remarks>
/// Unlike the other content roots the book index is optional. A campaign without one still runs, with
/// search reaching only its own content, so a missing directory is an empty result rather than an
/// error.
/// </remarks>
public static class BookIndexFileLocator
{
    /// <summary>
    /// Resolves the book index directory. When <paramref name="configuredPath"/> points at an existing
    /// directory it is used directly; otherwise <c>data/book-index</c> under
    /// <paramref name="contentRoot"/> is, if it exists; otherwise the search walks up the directory
    /// tree from <paramref name="startDirectory"/> looking for a <c>data/book-index</c> folder.
    /// </summary>
    /// <param name="startDirectory">Directory to begin the upward search from.</param>
    /// <param name="configuredPath">Optional configured path (absolute or relative to the start directory).</param>
    /// <param name="contentRoot">
    /// Optional shared content root (<c>Content:Root</c>) holding every kind of content in its default
    /// layout; absolute or relative to the start directory.
    /// </param>
    /// <returns>The full path to an existing book index directory, or an empty string when there is none.</returns>
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
            string candidate = Path.Combine(Path.GetFullPath(contentRoot, startDirectory), "data", "book-index");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        for (DirectoryInfo dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "data", "book-index");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }
}
