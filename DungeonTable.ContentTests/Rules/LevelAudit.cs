using System.Collections.Generic;
using System.Globalization;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Dossiers;
using DungeonTable.Infrastructure.Links;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules for a level file: the store can place it and every area in it, nothing in it is written
/// twice, and every area can be reached, by a link and from the DM panel.
/// </summary>
internal static class LevelAudit
{
    /// <summary>
    /// What the store would load without being able to place: a file with no floor number, a level with
    /// no id, and an area with no id, which the store drops.
    /// </summary>
    /// <param name="fileName">The level file's name ("level-1.json").</param>
    /// <param name="set">The level file, as written.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> Unplaced(string fileName, LevelDossierSet set)
    {
        var faults = new List<string>();
        if (!FileSystemDossierStore.TryFloorNumber(fileName, out _))
        {
            faults.Add($"{fileName} is not named level-<n>.json with n from 1, so no [[L<n> area …]] link, exit or quest can reach its areas.");
        }

        Check.Written(faults, set.Level?.LevelNodeId, "It declares no levelNodeId, so its areas belong to no floor and no [[area …]] link can name them.");
        for (int i = 0; i < set.Areas.Count; i++)
        {
            Check.Written(faults, set.Areas[i]?.AreaNodeId, $"areas[{i}] ('{set.Areas[i]?.Title}') has no areaNodeId, so the app drops it.");
        }

        return faults;
    }

    /// <summary>
    /// Every level id and area id this file repeats, or shares with another level file. The store keeps
    /// the last of them, so the others are silently gone.
    /// </summary>
    /// <param name="fileName">This file's name.</param>
    /// <param name="set">This file, as written.</param>
    /// <param name="others">The other level files, by name.</param>
    /// <returns>One sentence per id (empty when there are none).</returns>
    internal static IReadOnlyList<string> WrittenTwice(string fileName, LevelDossierSet set, IEnumerable<(string File, LevelDossierSet Set)> others)
    {
        (string File, LevelDossierSet Set)[] elsewhere = others
            .Where(other => !string.Equals(other.File, fileName, StringComparison.Ordinal))
            .ToArray();

        var faults = new List<string>();
        string level = set.Level?.LevelNodeId ?? string.Empty;
        faults.AddRange(elsewhere
            .Where(other => level.Length > 0 && string.Equals(other.Set.Level?.LevelNodeId, level, StringComparison.Ordinal))
            .Select(other => $"{other.File} declares the same level id, '{level}': the app keeps only one of the two."));

        string[] areaIds = AreaIds(set).ToArray();
        faults.AddRange(Check.Repeated(areaIds).Select(id => $"The area id '{id}' is written more than once in {fileName}: the app keeps only the last."));
        foreach ((string file, LevelDossierSet other) in elsewhere)
        {
            var theirs = new HashSet<string>(AreaIds(other), StringComparer.Ordinal);
            faults.AddRange(areaIds.Distinct(StringComparer.Ordinal).Where(theirs.Contains)
                .Select(id => $"The area id '{id}' is also in {file}: the app keeps only one of them."));
        }

        return faults;
    }

    /// <summary>
    /// Every area in the file that an <c>[[area …]]</c> link on its floor does not reach, by the key its
    /// title prints ("(Level 1, Area 3b)") or, failing that, its area number.
    /// </summary>
    /// <param name="set">The level file, as written.</param>
    /// <param name="targets">What a link can name in the loaded content.</param>
    /// <returns>One sentence per area (empty when there are none).</returns>
    internal static IReadOnlyList<string> Unreachable(LevelDossierSet set, LinkTargets targets)
    {
        string floor = set.Level?.LevelNodeId ?? string.Empty;
        if (floor.Length == 0)
        {
            // Unplaced says so: a floor with no id has nothing to read a link on.
            return Array.Empty<string>();
        }

        var faults = new List<string>();
        foreach (AreaDossier area in set.Areas.Where(area => !string.IsNullOrWhiteSpace(area?.AreaNodeId)))
        {
            string key = LinkTargets.PrintedKeyOf(area.Title);
            if (key.Length == 0 && area.AreaNumber > 0)
            {
                key = area.AreaNumber.ToString(CultureInfo.InvariantCulture);
            }

            if (key.Length == 0)
            {
                faults.Add($"The area '{area.AreaNodeId}' has no key to link it by: its title prints no 'Area N', and its areaNumber is 0.");
                continue;
            }

            Result<LinkTarget> found = targets.Resolve($"area {key}", floor);
            if (!found.IsValid)
            {
                faults.Add($"[[area {key}]] does not reach the area '{area.AreaNodeId}': {Check.Messages(found)}");
            }
            else if (!string.Equals(found.Value.TargetId, area.AreaNodeId, StringComparison.Ordinal))
            {
                faults.Add($"[[area {key}]] reaches '{found.Value.TargetId}', not the area '{area.AreaNodeId}' whose title prints that key.");
            }
        }

        return faults;
    }

    /// <summary>Every area in the file whose briefing the DM panel cannot open.</summary>
    /// <param name="set">The level file, as written.</param>
    /// <param name="projection">The briefings, cards and search built from the loaded content.</param>
    /// <returns>One sentence per area (empty when there are none).</returns>
    internal static IReadOnlyList<string> Unbriefed(LevelDossierSet set, IContentProjection projection) =>
        AreaIds(set)
            .Select(id => (Id: id, Briefing: projection.GetBriefing(id)))
            .Where(area => !area.Briefing.IsValid)
            .Select(area => $"The area '{area.Id}' opens no briefing: {Check.Messages(area.Briefing)}")
            .ToList();

    private static IEnumerable<string> AreaIds(LevelDossierSet set) =>
        set.Areas.Select(area => area?.AreaNodeId).Where(id => !string.IsNullOrWhiteSpace(id));
}
