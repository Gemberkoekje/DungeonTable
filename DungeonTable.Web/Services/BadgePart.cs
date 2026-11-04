namespace DungeonTable.Web.Services;

/// <summary>One badge beside a room's label: what it means, what it shows, and what it says on hover.</summary>
/// <param name="Kind">What the badge says.</param>
/// <param name="Text">What it shows: a glyph, and for creatures how many ("☠ 3").</param>
/// <param name="Title">The same in words, for a tooltip or a screen reader.</param>
public sealed record BadgePart(BadgeKind Kind, string Text, string Title);
