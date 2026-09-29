using System.IO;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;

namespace DungeonTable.Tests;

/// <summary>
/// A throwaway copy of the sample pack that a test may edit, for running the app on content the pack
/// itself must never hold: a broken document, a hand-written <c>null</c>.
/// </summary>
internal sealed class PackCopy : IDisposable
{
    public PackCopy()
    {
        Root = Path.Combine(Path.GetTempPath(), "dt-pack-" + Guid.NewGuid().ToString("N"));
        foreach (string file in Directory.EnumerateFiles(SamplePack.Root, "*", SearchOption.AllDirectories))
        {
            string copy = Path.Combine(Root, Path.GetRelativePath(SamplePack.Root, file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            File.Copy(file, copy);
        }
    }

    /// <summary>The copy's folder, a content root.</summary>
    public string Root { get; }

    /// <summary>Points a test host at the copy, as <see cref="SamplePack.Serve"/> does at the pack.</summary>
    /// <param name="builder">The test host's builder.</param>
    public void Serve(IWebHostBuilder builder)
    {
        SamplePack.Serve(builder);
        builder.UseSetting("Content:Root", Root);
    }

    /// <summary>
    /// Writes <c>null</c> in place of one value of a document: a property ("areas[0].title") or a list
    /// entry ("regions[0]").
    /// </summary>
    /// <param name="relative">The document, from the content root.</param>
    /// <param name="path">Where in it, dotted, with [n] for a list entry.</param>
    public void SetNull(string relative, string path)
    {
        string file = Path.Combine(Root, relative);
        JsonNode document = JsonNode.Parse(File.ReadAllText(file));
        JsonNode parent = document;
        string[] steps = path.Split('.');
        for (int i = 0; i < steps.Length; i++)
        {
            (string name, int index) = Step(steps[i]);
            bool last = i == steps.Length - 1;
            if (index < 0)
            {
                if (last)
                {
                    parent.AsObject()[name] = null;
                }
                else
                {
                    parent = parent[name];
                }
            }
            else
            {
                JsonArray list = (name.Length > 0 ? parent[name] : parent).AsArray();
                if (last)
                {
                    list[index] = null;
                }
                else
                {
                    parent = list[index];
                }
            }
        }

        File.WriteAllText(file, document.ToJsonString());
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    // "areas[0]" is ("areas", 0); "title" is ("title", -1).
    private static (string Name, int Index) Step(string step)
    {
        int open = step.IndexOf('[', StringComparison.Ordinal);
        return open < 0
            ? (step, -1)
            : (step[..open], int.Parse(step[(open + 1)..^1], System.Globalization.CultureInfo.InvariantCulture));
    }
}
