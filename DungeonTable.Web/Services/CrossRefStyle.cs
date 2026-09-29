using DungeonTable.Core.Dossier;

namespace DungeonTable.Web.Services;

/// <summary>
/// Presentation helpers for a <see cref="CrossRefKind"/>: the CSS-class suffix that colours a link,
/// breadcrumb chip or reference card by kind, and a human-readable kind name for tooltips. Shared so
/// prose links, the breadcrumb trail and the reference drawer stay colour-consistent.
/// </summary>
public static class CrossRefStyle
{
    /// <summary>The CSS-class suffix for a kind ("monster", "spell", ...), used as <c>xref-{suffix}</c>.</summary>
    /// <param name="kind">The reference kind.</param>
    /// <returns>The lower-case suffix.</returns>
    public static string CssClass(CrossRefKind kind) => kind switch
    {
        CrossRefKind.Area => "area",
        CrossRefKind.Monster => "monster",
        CrossRefKind.Spell => "spell",
        CrossRefKind.Item => "item",
        CrossRefKind.Npc => "npc",
        CrossRefKind.Faction => "faction",
        CrossRefKind.SkillCheck => "check",
        CrossRefKind.Character => "character",
        CrossRefKind.Unresolved => "unresolved",
        CrossRefKind.Pending => "pending",
        CrossRefKind.Book => "book",
        _ => "other",
    };

    /// <summary>A human-readable name for a kind ("Monster", "Item", ...), for tooltips and card headings.</summary>
    /// <param name="kind">The reference kind.</param>
    /// <returns>The display name.</returns>
    public static string KindName(CrossRefKind kind) => kind switch
    {
        CrossRefKind.Area => "Area",
        CrossRefKind.Monster => "Monster",
        CrossRefKind.Spell => "Spell",
        // Not "Magic item": an authored item can be a stone key or a copper helm that isn't magic.
        CrossRefKind.Item => "Item",
        CrossRefKind.Npc => "NPC",
        CrossRefKind.Faction => "Faction",
        CrossRefKind.SkillCheck => "Check",
        CrossRefKind.Character => "Player character",
        CrossRefKind.Unresolved => "Broken link",
        CrossRefKind.Pending => "Not authored yet",
        CrossRefKind.Book => "From the book",
        _ => "Reference",
    };
}
