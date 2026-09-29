namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// The file names a store reads its documents under, matched the way Linux, where the app is usually
/// deployed, matches them: exactly, casing and all. Shared by the stores, which report a <c>.json</c>
/// they would not read, and the content tests.
/// </summary>
internal static class DocumentNames
{
    /// <summary>Whether a file name matches a store's name or pattern.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="pattern">A name, or a pattern with one <c>*</c> ("level-*.json").</param>
    /// <returns>True when it matches.</returns>
    internal static bool Matches(string fileName, string pattern)
    {
        int star = pattern.IndexOf('*', StringComparison.Ordinal);
        if (star < 0)
        {
            return string.Equals(fileName, pattern, StringComparison.Ordinal);
        }

        string prefix = pattern[..star];
        string suffix = pattern[(star + 1)..];
        return fileName.Length >= prefix.Length + suffix.Length
            && fileName.StartsWith(prefix, StringComparison.Ordinal)
            && fileName.EndsWith(suffix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every <c>.json</c> file at the top of a folder that none of its store's names match: most likely
    /// a document meant for the app, under a name that is slightly off.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="patterns">The names and patterns its store reads.</param>
    /// <returns>A problem for each, in file-name order.</returns>
    internal static IReadOnlyList<ContentProblem> Unread(string folder, IReadOnlyList<string> patterns)
    {
        if (!Directory.Exists(folder))
        {
            return Array.Empty<ContentProblem>();
        }

        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Where(path => !patterns.Any(pattern => Matches(Path.GetFileName(path), pattern)))
            .Select(path => new ContentProblem(path, UnreadProblem(Path.GetFileName(path), patterns)))
            .ToList();
    }

    // A name that matches once case is ignored is read on a Windows machine and not on the Linux server.
    private static string UnreadProblem(string name, IReadOnlyList<string> patterns)
    {
        string casing = patterns.Any(pattern => Matches(name.ToLowerInvariant(), pattern))
            ? " Names are matched exactly, casing and all, on Linux, so write it in lower case."
            : string.Empty;
        return $"the app reads no file by this name, so it is skipped.{casing} The documents in its folder are named {string.Join(", ", patterns)}.";
    }
}
