using System.Collections.Generic;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules for a stat-block or spell document: every entry has an id of its own in its document, every
/// monster's numbers are ones a monster can have, every spell it casts is in the library, and every
/// source is a book the book index names. A document is checked on its own, as written; that a
/// campaign's block replaces a published one under the same id, across documents, is intended.
/// </summary>
internal static class StatAudit
{
    // The legal challenge ratings, low to high.
    private static readonly HashSet<string> LegalChallengeRatings = new(StringComparer.Ordinal)
    {
        "0", "1/8", "1/4", "1/2",
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
        "11", "12", "13", "14", "15", "16", "17", "18", "19", "20",
        "21", "22", "23", "24", "25", "26", "27", "28", "29", "30",
    };

    /// <summary>
    /// Every entry with no node id, which the library drops, and every node id the document gives two
    /// entries, of which the library keeps only the last. The id is what links, creature lists, art and
    /// saved fights name a block by.
    /// </summary>
    /// <param name="entries">The document's entries, as id and name.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> Unidentified(IReadOnlyList<(string NodeId, string Name)> entries)
    {
        var faults = entries
            .Select((entry, i) => (entry, i))
            .Where(item => string.IsNullOrWhiteSpace(item.entry.NodeId))
            .Select(item => $"[{item.i}] ('{item.entry.Name}') has no nodeId, so the library drops it.")
            .ToList();

        faults.AddRange(Check.Repeated(entries.Select(entry => entry.NodeId))
            .Select(id => $"The nodeId '{id}' is given to two entries: the library keeps only the last."));
        return faults;
    }

    /// <summary>Every monster with a blank name, or an AC, hit points or ability score no monster has.</summary>
    /// <param name="monsters">The document's monsters.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> Implausible(IEnumerable<StatBlock> monsters)
    {
        var faults = new List<string>();
        foreach (StatBlock monster in monsters)
        {
            string which = $"'{monster.NodeId}'";
            Check.Written(faults, monster.Name, $"{which} has no name.");
            if (monster.ArmourClass is < 1 or > 30)
            {
                faults.Add($"{which} has AC {monster.ArmourClass}; write 1 to 30.");
            }

            if (monster.AverageHitPoints <= 0)
            {
                faults.Add($"{which} has {monster.AverageHitPoints} hit points.");
            }

            AbilityScores scores = monster.Abilities ?? new AbilityScores();
            faults.AddRange(new[]
                {
                    (Ability: "Str", Score: scores.Str), (Ability: "Dex", Score: scores.Dex), (Ability: "Con", Score: scores.Con),
                    (Ability: "Int", Score: scores.Intelligence), (Ability: "Wis", Score: scores.Wis), (Ability: "Cha", Score: scores.Cha),
                }
                .Where(ability => ability.Score is < 1 or > 30)
                .Select(ability => $"{which} has {ability.Ability} {ability.Score}; write 1 to 30."));
        }

        return faults;
    }

    /// <summary>
    /// Every monster whose challenge rating is not a legal one, or whose proficiency bonus is not the
    /// one that challenge rating gives.
    /// </summary>
    /// <param name="monsters">The document's monsters.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> WrongChallenge(IEnumerable<StatBlock> monsters)
    {
        var faults = new List<string>();
        foreach (StatBlock monster in monsters)
        {
            if (!LegalChallengeRatings.Contains(monster.ChallengeRating ?? string.Empty))
            {
                faults.Add($"'{monster.NodeId}' has challenge rating '{monster.ChallengeRating}', which is no legal one.");
            }
            else if (monster.ProficiencyBonus != ProficiencyBonus(monster.ChallengeRating))
            {
                faults.Add($"'{monster.NodeId}' has proficiency bonus {monster.ProficiencyBonus}; challenge rating {monster.ChallengeRating} gives {ProficiencyBonus(monster.ChallengeRating)}.");
            }
        }

        return faults;
    }

    /// <summary>Every trait, action, bonus action, reaction or legendary action with no text to read.</summary>
    /// <param name="monsters">The document's monsters.</param>
    /// <returns>One sentence per entry (empty when there are none).</returns>
    internal static IReadOnlyList<string> EmptyEntries(IEnumerable<StatBlock> monsters) =>
        monsters
            .SelectMany(monster => monster.Traits
                .Concat(monster.Actions)
                .Concat(monster.BonusActions)
                .Concat(monster.Reactions)
                .Concat(monster.LegendaryActions)
                .Where(entry => string.IsNullOrWhiteSpace(entry?.Text))
                .Select(entry => $"'{monster.NodeId}' has an entry '{entry?.Name}' with no text."))
            .ToList();

    /// <summary>Every spell a monster casts by node id that the library does not have.</summary>
    /// <param name="monsters">The document's monsters.</param>
    /// <param name="stats">The loaded library.</param>
    /// <returns>One sentence per spell (empty when there are none).</returns>
    internal static IReadOnlyList<string> UnextractedSpells(IEnumerable<StatBlock> monsters, IStatLibrary stats) =>
        monsters
            .SelectMany(monster => (monster.Spellcasting?.Spells ?? Array.Empty<SpellReference>())
                .Where(spell => !string.IsNullOrEmpty(spell.NodeId) && !stats.GetSpell(spell.NodeId).IsValid)
                .Select(spell => $"'{monster.NodeId}' casts '{spell.Name}' as '{spell.NodeId}', which the library does not have."))
            .ToList();

    /// <summary>
    /// Every source an entry cites that no book index file names with a title. The citation then shows
    /// the raw file name ("SRD_CC_v5.1 p.266") instead of the book's ("SRD 5.1 p.266").
    /// </summary>
    /// <param name="sources">The document's entries, as id and cited source.</param>
    /// <param name="books">The loaded book index.</param>
    /// <returns>One sentence per source (empty when there are none).</returns>
    internal static IReadOnlyList<string> UntitledSources(IEnumerable<(string NodeId, string Source)> sources, IEnumerable<BookIndex> books)
    {
        var titled = new HashSet<string>(
            books.Where(book => !string.IsNullOrWhiteSpace(book.Source) && !string.IsNullOrWhiteSpace(book.Title))
                .Select(book => book.Source.Trim()),
            StringComparer.OrdinalIgnoreCase);

        return sources
            .Select(entry => (entry.NodeId, Source: (entry.Source ?? string.Empty).Trim()))
            .Where(entry => entry.Source.Length > 0 && !titled.Contains(entry.Source))
            .GroupBy(entry => entry.Source, StringComparer.Ordinal)
            .Select(group => $"'{group.Key}' is cited by {group.Count()} entries ('{group.First().NodeId}' first), and no book index file names it with a title.")
            .ToList();
    }

    private static int ProficiencyBonus(string challengeRating) => challengeRating switch
    {
        "0" or "1/8" or "1/4" or "1/2" or "1" or "2" or "3" or "4" => 2,
        "5" or "6" or "7" or "8" => 3,
        "9" or "10" or "11" or "12" => 4,
        "13" or "14" or "15" or "16" => 5,
        "17" or "18" or "19" or "20" => 6,
        "21" or "22" or "23" or "24" => 7,
        "25" or "26" or "27" or "28" => 8,
        _ => 9,
    };
}
