using System;
using System.Collections.Generic;

namespace DungeonTable.Web.Services;

/// <summary>
/// <c>@key</c>s for a list of authored items, unique even when the ids they are made from are not.
/// </summary>
/// <remarks>
/// <para>
/// Blazor requires the keys of sibling elements to be unique, and throws on a duplicate. An NPC's,
/// a character's or a scene's id is written by hand, and nothing on the way in makes it unique or
/// non-blank, so two NPCs sharing an id (or two left without one) took the whole tab down with them.
/// </para>
/// <para>
/// The key is the id and how many times that id has come up before in the list, so a well-formed list
/// is keyed by its ids exactly as before, and the second of two items with one id is still told apart.
/// Build the keys over the whole list, then filter it: a filter that hides the first of two duplicates
/// must not hand the second one the first one's key.
/// </para>
/// </remarks>
public static class RenderKeys
{
    /// <summary>Pairs each item with a key unique within the list.</summary>
    /// <typeparam name="T">The items' type.</typeparam>
    /// <param name="items">The items, in the order they are rendered.</param>
    /// <param name="idOf">The authored id of an item; blank for an item written without one.</param>
    /// <returns>The items with their keys, in the same order.</returns>
    public static IReadOnlyList<Keyed<T>> Of<T>(IEnumerable<T> items, Func<T, string> idOf)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(idOf);

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var keyed = new List<Keyed<T>>();
        foreach (T item in items)
        {
            string id = idOf(item) ?? string.Empty;
            seen.TryGetValue(id, out int before);
            seen[id] = before + 1;
            keyed.Add(new Keyed<T>(item, (id, before)));
        }

        return keyed;
    }
}
