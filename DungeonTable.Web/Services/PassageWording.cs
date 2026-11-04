using DungeonTable.Core.Dossier;

namespace DungeonTable.Web.Services;

/// <summary>The words the DM screen uses for where a passage or a found name sits.</summary>
public static class PassageWording
{
    /// <summary>The badge Find anything shows beside a passage.</summary>
    /// <param name="kind">Where the passage sits.</param>
    /// <returns>The badge's word.</returns>
    public static string Category(PassageKind kind) => kind switch
    {
        PassageKind.Rule => "rules",
        PassageKind.Level => "floor",
        PassageKind.Area => "room",
        PassageKind.Npc => "npc",
        PassageKind.Entity => "entity",
        PassageKind.Quest => "quest",
        PassageKind.Session => "session",
        PassageKind.Deck => "deck",
        PassageKind.Campaign => "campaign",
        PassageKind.Party => "party",
        PassageKind.Story => "story",
        _ => "text",
    };

    /// <summary>
    /// The badge beside something found by its name: the search's own category, except that an area is
    /// called a room, as everywhere else on the DM screen.
    /// </summary>
    /// <param name="category">The <see cref="SearchMatch.Category"/>.</param>
    /// <returns>The badge's word.</returns>
    public static string NameCategory(string category) =>
        string.Equals(category, SearchMatch.AreaCategory, System.StringComparison.Ordinal) ? "room" : category ?? string.Empty;
}
