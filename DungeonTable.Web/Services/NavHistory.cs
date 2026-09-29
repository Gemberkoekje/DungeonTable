using System;
using System.Collections.Generic;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Web.Services;

/// <summary>
/// The DM's browser-like reference history: one ordered list of <see cref="NavEntry"/> stops with a
/// movable cursor, so Back / Forward and the breadcrumb trail all read from a single source. Areas
/// and entities share the list, so a chain like room → monster → its spell is one trail; the panel
/// follows the nearest area at or before the cursor while a card is open.
/// </summary>
public sealed class NavHistory
{
    private readonly List<NavEntry> entries = new List<NavEntry>();
    private int cursor = -1;

    /// <summary>Every stop recorded so far, oldest first.</summary>
    public IReadOnlyList<NavEntry> Entries => entries;

    /// <summary>Index of the current stop, or -1 when the history is empty.</summary>
    public int Cursor => cursor;

    /// <summary>The current stop; an empty entry when the history is empty.</summary>
    public NavEntry Current => cursor >= 0 ? entries[cursor] : default;

    /// <summary>True when there is an earlier stop to go back to.</summary>
    public bool CanBack => cursor > 0;

    /// <summary>True when there is a later stop to go forward to.</summary>
    public bool CanForward => cursor >= 0 && cursor < entries.Count - 1;

    /// <summary>
    /// Appends a stop: any entries ahead of the cursor are dropped (a new branch replaces the
    /// forward trail), then the cursor points at the new stop. Re-selecting the current target is a
    /// no-op, so the breadcrumb does not grow duplicate chips.
    /// </summary>
    /// <param name="entry">The stop to record.</param>
    /// <returns><c>true</c> when the history changed.</returns>
    public bool Push(NavEntry entry)
    {
        if (cursor < entries.Count - 1)
        {
            entries.RemoveRange(cursor + 1, entries.Count - 1 - cursor);
        }

        if (cursor >= 0
            && string.Equals(entries[cursor].NodeId, entry.NodeId, StringComparison.Ordinal)
            && entries[cursor].Kind == entry.Kind)
        {
            return false;
        }

        entries.Add(entry);
        cursor = entries.Count - 1;
        return true;
    }

    /// <summary>Moves the cursor to the previous stop.</summary>
    /// <returns><c>true</c> when the cursor moved.</returns>
    public bool Back()
    {
        if (!CanBack)
        {
            return false;
        }

        cursor--;
        return true;
    }

    /// <summary>Moves the cursor to the next stop.</summary>
    /// <returns><c>true</c> when the cursor moved.</returns>
    public bool Forward()
    {
        if (!CanForward)
        {
            return false;
        }

        cursor++;
        return true;
    }

    /// <summary>Moves the cursor to a specific stop (a breadcrumb chip click).</summary>
    /// <param name="index">Index into <see cref="Entries"/>.</param>
    /// <returns><c>true</c> when the index was in range.</returns>
    public bool JumpTo(int index)
    {
        if (index < 0 || index >= entries.Count)
        {
            return false;
        }

        cursor = index;
        return true;
    }

    /// <summary>
    /// Index of the nearest <see cref="CrossRefKind.Area"/> stop at or before the cursor — the room
    /// the panel keeps showing while a reference card is open — or -1 when there is none.
    /// </summary>
    /// <returns>The index, or -1.</returns>
    public int NearestAreaIndex()
    {
        for (int i = cursor; i >= 0; i--)
        {
            if (entries[i].Kind == CrossRefKind.Area)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The node id of the nearest area at or before the cursor, or an empty string.</summary>
    /// <returns>The area's node id, or an empty string.</returns>
    public string NearestArea()
    {
        int index = NearestAreaIndex();
        return index >= 0 ? entries[index].NodeId : string.Empty;
    }
}
