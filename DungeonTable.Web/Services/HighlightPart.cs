namespace DungeonTable.Web.Services;

/// <summary>A stretch of a Find result's text, and whether it is one of the words looked for.</summary>
/// <param name="Text">The stretch.</param>
/// <param name="Marked">True when it matched a word of the query.</param>
public readonly record struct HighlightPart(string Text, bool Marked);
