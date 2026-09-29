using System.IO;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>
/// A throwaway content root under the temp folder, for proving the document rules on real files. It
/// is deleted when disposed.
/// </summary>
internal sealed class TempContent : IDisposable
{
    public TempContent()
    {
        Root = Path.Combine(Path.GetTempPath(), "dungeontable-content-rules-" + Guid.NewGuid().ToString("N"));
        Start = Path.Combine(Root, "start");
        Directory.CreateDirectory(Start);
    }

    /// <summary>The content root.</summary>
    public string Root { get; }

    /// <summary>An empty folder for the locators' walk-up to start from.</summary>
    public string Start { get; }

    /// <summary>Writes a file under the content root, creating its folders.</summary>
    /// <param name="relativePath">Its path under the root, with forward slashes.</param>
    /// <param name="content">What it holds.</param>
    /// <returns>Its full path.</returns>
    public string Write(string relativePath, string content)
    {
        string path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A document under the content root, as the listing would describe it.</summary>
    /// <param name="relativePath">Its path under the root.</param>
    /// <param name="kind">What the app reads it as.</param>
    /// <returns>The document.</returns>
    public ContentDocument Document(string relativePath, DocumentKind kind) =>
        new(relativePath, Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)), kind);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A file still open on Windows; the temp folder is cleaned up eventually anyway.
        }
    }
}
