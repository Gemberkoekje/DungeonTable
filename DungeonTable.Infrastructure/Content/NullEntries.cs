namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// Finds every <c>null</c> entry in a list, anywhere in a document: the entries the stores drop without
/// a word, since they hold nothing to show. Shared by the stores, which say so in the app's log, and the
/// content tests, which fail on them.
/// </summary>
internal static class NullEntries
{
    // How the stores read a document: comments skipped, trailing commas allowed.
    private static readonly JsonDocumentOptions AsTheStoresRead = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Where each <c>null</c> entry is, from the document's root: "areas[0]", "quests[1].beats[0]", or
    /// "[3]" in a document that is a list. A <c>null</c> list is not one: it reads as empty, as
    /// <c>[]</c> would.
    /// </summary>
    /// <param name="json">A document that parses.</param>
    /// <returns>The places, in document order (empty when there are none).</returns>
    internal static IReadOnlyList<string> In(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, AsTheStoresRead);
        var found = new List<string>();
        Walk(document.RootElement, string.Empty, found);
        return found;
    }

    /// <summary>The same, as sentences a log or a test report can show.</summary>
    /// <param name="json">A document that parses.</param>
    /// <returns>"areas[0] is null, so the app drops it." for each (empty when there are none).</returns>
    internal static IReadOnlyList<string> Described(string json) =>
        In(json).Select(at => $"{at} is null, so the app drops it.").ToList();

    private static void Walk(JsonElement element, string path, List<string> found)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Walk(property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}", found);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (JsonElement entry in element.EnumerateArray())
            {
                string at = $"{path}[{i}]";
                if (entry.ValueKind == JsonValueKind.Null)
                {
                    found.Add(at);
                }
                else
                {
                    Walk(entry, at, found);
                }

                i++;
            }
        }
    }
}
