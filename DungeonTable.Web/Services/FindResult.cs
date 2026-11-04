namespace DungeonTable.Web.Services;

/// <summary>
/// One row of Find anything: something found by its name (it opens as a link would, a room on the Room
/// tab and anything else as a card), or a passage found by its words, shown with the words around the
/// match so the row itself is often the answer (it opens as a sheet over the dock).
/// </summary>
public sealed class FindResult
{
    /// <summary>What the row is, and so what opening it does.</summary>
    public FindResultKind Kind { get; init; } = FindResultKind.None;

    /// <summary>The row's title: the thing's name, or the passage's heading.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>The badge beside the title ("rules", "room", "npc", "book").</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Where it is: a passage's trail, or where a book entry is printed; empty when nothing says.</summary>
    public string Where { get; init; } = string.Empty;

    /// <summary>For a passage, its words around the match; empty for a name.</summary>
    public string Snippet { get; init; } = string.Empty;

    /// <summary>True when the passage is DM-only.</summary>
    public bool Secret { get; init; }

    /// <summary>For a name, the id to open; for a passage, its key.</summary>
    public string Target { get; init; } = string.Empty;
}
