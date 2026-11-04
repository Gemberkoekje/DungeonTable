using System;

namespace DungeonTable.Web.Services;

/// <summary>
/// What a pin names, as the text the table saves: <c>card:</c> and an id for a card (a monster, a
/// person, a player character's sheet, a spell, a book entry), or <c>text:</c> and a passage's key for a
/// rules section, a floor's fact or any passage Find anything found. The prefix keeps the two apart, since
/// a passage key and a card id are both just text.
/// </summary>
public static class PinKeys
{
    private const string CardPrefix = "card:";
    private const string TextPrefix = "text:";

    /// <summary>The pin of a card.</summary>
    /// <param name="id">The id a link opens the card by.</param>
    /// <returns>The pin.</returns>
    public static string Card(string id) => CardPrefix + (id ?? string.Empty).Trim();

    /// <summary>The pin of a passage.</summary>
    /// <param name="key">The passage's key.</param>
    /// <returns>The pin.</returns>
    public static string Passage(string key) => TextPrefix + (key ?? string.Empty).Trim();

    /// <summary>True when the pin names a card.</summary>
    /// <param name="pin">The pin.</param>
    /// <returns><c>true</c> for a card's pin.</returns>
    public static bool IsCard(string pin) => (pin ?? string.Empty).StartsWith(CardPrefix, StringComparison.Ordinal);

    /// <summary>True when the pin names a passage.</summary>
    /// <param name="pin">The pin.</param>
    /// <returns><c>true</c> for a passage's pin.</returns>
    public static bool IsPassage(string pin) => (pin ?? string.Empty).StartsWith(TextPrefix, StringComparison.Ordinal);

    /// <summary>What the pin names: the card's id or the passage's key, without the prefix.</summary>
    /// <param name="pin">The pin.</param>
    /// <returns>The id or key; an empty string for a pin that is neither.</returns>
    public static string Target(string pin)
    {
        if (IsCard(pin))
        {
            return pin[CardPrefix.Length..];
        }

        return IsPassage(pin) ? pin[TextPrefix.Length..] : string.Empty;
    }
}
