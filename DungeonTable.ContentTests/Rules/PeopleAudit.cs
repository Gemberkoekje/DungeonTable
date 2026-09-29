using System.Collections.Generic;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules for the people: every NPC and character card has what it shows, every contact and cast
/// row says where that person stands, and the party's two documents agree.
/// </summary>
internal static class PeopleAudit
{
    /// <summary>
    /// Every NPC missing something their card shows: who they are, where they are found, how they
    /// feel, and, the reason a DM opens the card mid-session, what they can tell the party.
    /// </summary>
    /// <param name="roster">The NPC roster.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> IncompleteNpcs(NpcRoster roster)
    {
        var faults = new List<string>();
        for (int i = 0; i < roster.Npcs.Count; i++)
        {
            NpcDossier npc = roster.Npcs[i];
            string who = $"The NPC {Check.Named(npc.Id, $"npcs[{i}]")}";
            Check.Written(faults, npc.Id, $"{who} has no id, so nothing can link to them.");
            Check.Written(faults, npc.Name, $"{who} has no name.");
            Check.Written(faults, npc.Role, $"{who} has no role.");
            Check.Written(faults, npc.Where, $"{who} says nowhere to find them.");
            Check.Written(faults, npc.Disposition, $"{who} has no disposition.");
            Check.Written(faults, npc.Summary, $"{who} has no summary.");
            if (npc.Knows.Count == 0)
            {
                faults.Add($"{who} knows nothing, and the card opens on what they can tell the party.");
            }

            if (npc.Hooks.Count == 0)
            {
                faults.Add($"{who} has no hooks.");
            }
        }

        faults.AddRange(Check.Repeated(roster.Npcs.Select(npc => npc.Id))
            .Select(id => $"Two NPCs have the id '{id}': their cards would collide, and a link could open only one."));
        return faults;
    }

    /// <summary>Every NPC contact that does not say who the person is, what they are, and where they stand.</summary>
    /// <param name="roster">The NPC roster.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> IncompleteContacts(NpcRoster roster) =>
        roster.Npcs
            .SelectMany(npc => npc.Contacts.Select((contact, i) => CastRowFaults(contact, $"npcs.json '{npc.Id}'.contacts[{i}]")))
            .SelectMany(faults => faults)
            .ToList();

    /// <summary>
    /// What a cast row or a contact is missing: the name, the role, or the state the person is in,
    /// which is what a DM reaches for when a player says the name out loud.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="where">Where it is, for the sentence.</param>
    /// <returns>One sentence per missing field.</returns>
    internal static IReadOnlyList<string> CastRowFaults(CastMember row, string where)
    {
        var faults = new List<string>();
        Check.Written(faults, row.Name, $"{where} has no name.");
        Check.Written(faults, row.Role, $"{where} ({row.Name}) has no role.");
        Check.Written(faults, row.State, $"{where} ({row.Name}) does not say where they stand.");
        return faults;
    }

    /// <summary>
    /// Every player character missing something their card shows: the numbers the DM calls for at the
    /// table, the abilities that change an encounter, their story, and their hooks.
    /// </summary>
    /// <param name="party">The party dossier.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> IncompleteCharacters(PartyDossier party)
    {
        var faults = new List<string>();
        for (int i = 0; i < party.Characters.Count; i++)
        {
            CharacterDossier character = party.Characters[i];
            string who = $"The character {Check.Named(character.Id, $"characters[{i}]")}";
            Check.Written(faults, character.Id, $"{who} has no id, so nothing can link to them.");
            Check.Written(faults, character.Name, $"{who} has no name.");
            Check.Written(faults, character.PlayerName, $"{who} has no player.");
            Check.Written(faults, character.Build, $"{who} has no build line.");
            Check.Written(faults, character.Speed, $"{who} has no speed.");
            Check.Written(faults, character.Backstory, $"{who} has no backstory.");
            faults.AddRange(new[]
                {
                    (Bad: character.Level is < 1 or > 20, Fault: $"{who} is level {character.Level}; a character is level 1 to 20."),
                    (Bad: character.ArmourClass <= 0, Fault: $"{who} has no AC."),
                    (Bad: character.MaxHp <= 0, Fault: $"{who} has no hit points."),
                    (Bad: character.PassivePerception <= 0, Fault: $"{who} has no passive Perception."),
                    (Bad: character.Abilities.Count == 0, Fault: $"{who} has no abilities: the ones that change an encounter belong on the card."),
                    (Bad: character.Hooks.Count == 0, Fault: $"{who} has no hooks."),
                }
                .Where(check => check.Bad)
                .Select(check => check.Fault));
        }

        faults.AddRange(Check.Repeated(party.Characters.Select(character => character.Id))
            .Select(id => $"Two characters have the id '{id}': their cards would collide, and a link could open only one."));
        return faults;
    }

    /// <summary>
    /// Every character the party roster seed disagrees with: the dossier is what the Party tab prints
    /// and the roster is what a fight starts from, so the DM must not read one number and roll another.
    /// An empty roster disagrees with nothing: the dossier then stands alone.
    /// </summary>
    /// <param name="party">The party dossier.</param>
    /// <param name="roster">The party roster seed.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> Disagreements(PartyDossier party, Party roster)
    {
        var faults = new List<string>();
        if (roster.Members.Count == 0)
        {
            return faults;
        }

        foreach (CharacterDossier character in party.Characters)
        {
            PartyMember[] members = roster.Members
                .Where(member => string.Equals(member.Name, character.Name, StringComparison.Ordinal))
                .ToArray();
            if (members.Length != 1)
            {
                faults.Add($"{character.Name} is in party.json {members.Length} times, not once.");
                continue;
            }

            PartyMember member = members[0];
            faults.AddRange(new[]
                {
                    (Field: "level", Dossier: character.Level, Roster: member.Level),
                    (Field: "AC", Dossier: character.ArmourClass, Roster: member.ArmourClass),
                    (Field: "hit points", Dossier: character.MaxHp, Roster: member.MaxHp),
                    (Field: "passive Perception", Dossier: character.PassivePerception, Roster: member.PassivePerception),
                    (Field: "initiative modifier", Dossier: character.InitiativeModifier, Roster: member.InitiativeModifier),
                }
                .Where(number => number.Dossier != number.Roster)
                .Select(number => $"{character.Name}'s {number.Field} is {number.Dossier} in characters.json and {number.Roster} in party.json."));

            if (!string.Equals(character.PlayerName, member.PlayerName, StringComparison.Ordinal))
            {
                faults.Add($"{character.Name}'s player is '{character.PlayerName}' in characters.json and '{member.PlayerName}' in party.json.");
            }
        }

        return faults;
    }

    /// <summary>Every ally whose stat block is not one the library has, so the Battle tab has none to show.</summary>
    /// <param name="allies">The ally roster seed.</param>
    /// <param name="stats">The loaded stat blocks.</param>
    /// <returns>One sentence per ally (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadAllyBlocks(AllyRoster allies, IStatLibrary stats) =>
        allies.Members
            .Where(ally => !string.IsNullOrWhiteSpace(ally.StatBlockNodeId) && !stats.HasMonster(ally.StatBlockNodeId))
            .Select(ally => $"{ally.Name}'s statBlockNodeId '{ally.StatBlockNodeId}' is no stat block the library has, so the Battle tab has none to show for them.")
            .ToList();
}
