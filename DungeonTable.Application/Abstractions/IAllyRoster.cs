using DungeonTable.Core.Battle;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Reads and writes the saved roster of recurring allies (companions, familiars, hirelings).
/// Mirrors <see cref="IPartyRoster"/> exactly, including its fail-soft <see cref="GetAllies"/>.
/// </summary>
public interface IAllyRoster
{
    /// <summary>
    /// Gets the saved allies. Fail-soft: a roster that has never been saved yields a valid, empty
    /// list rather than an error.
    /// </summary>
    /// <returns>The saved ally roster (possibly empty).</returns>
    Result<AllyRoster> GetAllies();

    /// <summary>Saves the ally roster, replacing whatever was stored before.</summary>
    /// <param name="roster">The roster to persist.</param>
    /// <returns><see cref="Result.OK"/> on success, or an error result describing why the write failed.</returns>
    Result Save(AllyRoster roster);
}
