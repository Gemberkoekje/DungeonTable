using DungeonTable.Core.Battle;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Reads and writes the saved party roster — the set of player characters that seeds every fight.
/// Unlike the read-only dossier and stat stores this port also <em>saves</em>: the roster is edited
/// in the app, never by hand, so the adapter owns writing the document back.
/// </summary>
public interface IPartyRoster
{
    /// <summary>
    /// Gets the saved party. Fail-soft: a roster that has never been saved yields a valid, empty
    /// party rather than an error, so a fresh install starts with an empty editor instead of a
    /// warning. Only a document that exists but cannot be read produces an error result.
    /// </summary>
    /// <returns>The saved party (possibly empty).</returns>
    Result<Party> GetParty();

    /// <summary>Saves the party roster, replacing whatever was stored before.</summary>
    /// <param name="party">The roster to persist.</param>
    /// <returns><see cref="Result.OK"/> on success, or an error result describing why the write failed.</returns>
    Result Save(Party party);
}
