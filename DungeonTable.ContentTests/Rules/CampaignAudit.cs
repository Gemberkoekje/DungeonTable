using System.Collections.Generic;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules for <c>campaign.json</c>: the area the DM screen opens on exists, and the level table
/// says what each level is and which of them the app has.
/// </summary>
internal static class CampaignAudit
{
    /// <summary>A start area that no level file writes. The DM screen would open on "not found".</summary>
    /// <param name="campaign">The campaign dossier.</param>
    /// <param name="dossiers">The loaded content.</param>
    /// <returns>One sentence, or none.</returns>
    internal static IReadOnlyList<string> MissingStartArea(CampaignDossier campaign, IDossierStore dossiers)
    {
        string start = (campaign.StartAreaNodeId ?? string.Empty).Trim();
        return start.Length == 0 || dossiers.GetArea(start).IsValid
            ? Array.Empty<string>()
            : new[] { $"startAreaNodeId '{start}' is no area a level file writes, so the DM screen would open on 'not found'." };
    }

    /// <summary>
    /// Every level-table row with no name or no character level, the two things the table shows, or a
    /// required character level no character can have. A row may leave that last one out.
    /// </summary>
    /// <param name="campaign">The campaign dossier.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> IncompleteLevelRows(CampaignDossier campaign)
    {
        var faults = new List<string>();
        for (int i = 0; i < campaign.Levels.Count; i++)
        {
            LevelSummary row = campaign.Levels[i];
            string where = $"levels[{i}] (level {row.Level})";
            Check.Written(faults, row.Name, $"{where} has no name.");
            Check.Written(faults, row.CharacterLevel, $"{where} gives no character level for the table to show.");
            if (row.RequiredLevel is < 0 or > 20)
            {
                faults.Add($"{where} requires character level {row.RequiredLevel}; write 1 to 20, or leave it out.");
            }
        }

        return faults;
    }

    /// <summary>
    /// Every numbered level-table row whose "authored" mark disagrees with the level files: marked with
    /// no <c>level-{n}.json</c> loaded, or loaded and not marked. The mark is how the table tells the DM
    /// what exists today.
    /// </summary>
    /// <param name="campaign">The campaign dossier.</param>
    /// <param name="floorsWithAFile">The floor numbers that have a level file.</param>
    /// <returns>One sentence per row (empty when there are none).</returns>
    internal static IReadOnlyList<string> WrongAuthoredMarks(CampaignDossier campaign, IReadOnlySet<int> floorsWithAFile) =>
        campaign.Levels
            .Where(row => row.Level > 0 && row.Authored != floorsWithAFile.Contains(row.Level))
            .Select(row => row.Authored
                ? $"Level {row.Level} ('{row.Name}') is marked authored, but no level-{row.Level}.json is loaded."
                : $"Level {row.Level} ('{row.Name}') has a level-{row.Level}.json, but its row is not marked authored.")
            .ToList();

    /// <summary>The floor numbers that have a level file, from the loaded content.</summary>
    /// <param name="dossiers">The loaded content.</param>
    /// <returns>The numbers.</returns>
    internal static IReadOnlySet<int> FloorsWithAFile(IDossierStore dossiers) =>
        dossiers.AllLevels()
            .Select(level => dossiers.GetLevelNumber(level.LevelNodeId))
            .Where(number => number.IsValid)
            .Select(number => number.Value)
            .ToHashSet();
}
